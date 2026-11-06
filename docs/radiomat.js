'use strict';
document.querySelectorAll('.preset').forEach(button => {
  button.addEventListener('click', () => {
    document.querySelectorAll('.preset').forEach(other => other.setAttribute('aria-pressed', String(other === button)));
    document.querySelector('[data-station]').textContent = button.textContent;
  });
});
(async () => {
  try {
    const response = await fetch('https://api.github.com/repos/matmiccode/radiomat-app/releases/latest', {headers: {Accept: 'application/vnd.github+json'}});
    if (!response.ok) return;
    const release = await response.json();
    if (release.draft || release.prerelease) return;
    const asset = (release.assets || []).find(a => /^radioMAT-Setup-\d+\.\d+\.\d+-x64\.exe$/.test(a.name));
    if (!asset || !asset.browser_download_url.startsWith('https://github.com/matmiccode/radiomat-app/releases/download/')) return;
    const en = document.documentElement.lang === 'en';
    document.querySelectorAll('[data-download]').forEach(a => {
      a.href = asset.browser_download_url;
      a.textContent = en ? 'Download radioMAT for Windows' : 'Pobierz radioMAT dla Windows';
    });
    document.querySelectorAll('[data-version]').forEach(e => { e.textContent = (en ? 'Version ' : 'Wersja ') + String(release.tag_name).replace(/^v/, ''); });
  } catch (_) { /* The page remains usable without the release API. */ }
})();
