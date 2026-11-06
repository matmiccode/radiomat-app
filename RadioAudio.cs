using System.IO;
using System.Net.Http;
using NAudio.Wave;

namespace Radiomat;

// HTTP and decoding run off the UI thread; only this session owns its device.
internal sealed class RadioAudio : IDisposable
{
    private readonly CancellationTokenSource _cancel = new();
    private WaveOutEvent? _output;
    private readonly object _gate = new();
    private float _volume;
    public long DecodedBytes { get; private set; }
    public event Action<string>? Status;
    public event Action<Exception>? Failed;
    public event Action<string?>? TrackChanged;
    public float Volume
    {
        get { lock (_gate) return _output?.Volume ?? _volume; }
        set { lock (_gate) { _volume = Math.Clamp(value, 0, 1); if (_output != null) _output.Volume = _volume; } }
    }

    public Task StartAsync(string url) => Task.Run(async () =>
    {
        try
        {
            using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("radioMAT/" + UpdateService.VersionText);
            client.DefaultRequestHeaders.TryAddWithoutValidation("Icy-MetaData", "1");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_cancel.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            response.EnsureSuccessStatusCode();
            using var network = await response.Content.ReadAsStreamAsync(_cancel.Token);
            using var registration = _cancel.Token.Register(network.Dispose);
            Stream audioStream = network;
            if (response.Headers.TryGetValues("icy-metaint", out var values)
                && int.TryParse(values.FirstOrDefault(), out int interval) && interval > 0 && interval <= 16 * 1024 * 1024)
            {
                TrackChanged?.Invoke(null);
                audioStream = new IcyMetadataStream(network, interval, title => TrackChanged?.Invoke(title));
            }
            else TrackChanged?.Invoke(string.Empty);
            using var stream = new FullReadStream(audioStream, _cancel.Token);
            var frame = Mp3Frame.LoadFromStream(stream) ?? throw new IOException(Strings.T("NotMp3"));
            var format = new Mp3WaveFormat(frame.SampleRate, frame.ChannelMode == ChannelMode.Mono ? 1 : 2,
                frame.FrameLength, frame.BitRate);
            using var decoder = new AcmMp3FrameDecompressor(format);
            var buffer = new BufferedWaveProvider(decoder.OutputFormat) { BufferDuration = TimeSpan.FromSeconds(6) };
            var output = new WaveOutEvent();
            try
            {
            output.Init(buffer);
            lock (_gate) { _output = output; output.Volume = _volume; }
            var pcm = new byte[65536];
            bool started = false;
            while (!_cancel.IsCancellationRequested)
            {
                var count = decoder.DecompressFrame(frame, pcm, 0);
                buffer.AddSamples(pcm, 0, count);
                DecodedBytes += count;
                if (!started && buffer.BufferedDuration.TotalMilliseconds >= 500)
                {
                    output.Play(); started = true; Status?.Invoke("Live");
                }
                while (buffer.BufferedDuration.TotalSeconds > 3)
                    await Task.Delay(40, _cancel.Token);
                frame = Mp3Frame.LoadFromStream(stream) ?? throw new IOException(Strings.T("AudioEnded"));
            }
            }
            finally { lock (_gate) { _output = null; output.Dispose(); } }
        }
        catch (Exception ex) when (!_cancel.IsCancellationRequested) { Failed?.Invoke(ex); }
        catch (Exception) when (_cancel.IsCancellationRequested) { }
    });

    public void Dispose() { _cancel.Cancel(); lock (_gate) _output?.Stop(); }
}

// MP3 parsing requires complete reads and a position even on a network stream.
internal sealed class FullReadStream(Stream source, CancellationToken cancel) : Stream
{
    private long _position;
    public override int Read(byte[] buffer, int offset, int count)
    {
        int total = 0;
        while (total < count)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            deadline.CancelAfter(TimeSpan.FromSeconds(15));
            int read = source.ReadAsync(buffer.AsMemory(offset + total, count - total), deadline.Token).AsTask().GetAwaiter().GetResult();
            if (read == 0) break;
            total += read;
        }
        _position += total;
        return total;
    }
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => _position; set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
