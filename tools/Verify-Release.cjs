'use strict';
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const [directory, configPath] = process.argv.slice(2);
try {
  const config = JSON.parse(fs.readFileSync(configPath, 'utf8'));
  const manifest = JSON.parse(fs.readFileSync(path.join(directory, 'najnowsza.json'), 'utf8'));
  if (manifest.program !== 'radioMAT' || !/^\d{1,4}\.\d{1,4}\.\d{1,4}$/.test(manifest.wersja)
      || manifest.instalator !== `radioMAT-Setup-${manifest.wersja}-x64.exe` || typeof manifest.opis !== 'string'
      || !manifest.opis.trim() || manifest.opis.length > 12000) throw Error();
  const installer = path.join(directory, manifest.instalator);
  const bytes = fs.readFileSync(installer);
  if (bytes.length !== manifest.rozmiar || bytes.length < 1024 || bytes.length > 256 * 1024 * 1024) throw Error();
  const checksum = fs.readFileSync(installer + '.sha256', 'utf8').trim().split(/\s+/);
  if (checksum.length !== 2 || checksum[1] !== manifest.instalator
      || checksum[0] !== crypto.createHash('sha256').update(bytes).digest('hex')) throw Error();
  const raw = Buffer.from(config.UpdatePublicKey, 'base64');
  if (raw.length !== 32) throw Error();
  const key = crypto.createPublicKey({key: Buffer.concat([Buffer.from('302a300506032b6570032100', 'hex'), raw]), type: 'spki', format: 'der'});
  const signatureText = fs.readFileSync(installer + '.podpis', 'utf8').trim();
  if (!/^[A-Za-z0-9+/]{86}==$/.test(signatureText)) throw Error();
  const signature = Buffer.from(signatureText, 'base64');
  if (signature.length !== 64 || !crypto.verify(null, bytes, key, signature)) throw Error();
  console.log('OK: installer name, version description, size, SHA256 and Ed25519 signature match this edition.');
} catch (_) { console.error('Release verification failed. Nothing was published.'); process.exitCode = 1; }
