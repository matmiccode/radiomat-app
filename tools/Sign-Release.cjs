'use strict';
// Uses an external private key only inside Node's crypto API. Never prints it.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const [installer, keyPath, configPath, notesPath] = process.argv.slice(2);
try {
  if (!installer || !keyPath || !configPath || !notesPath) throw Error('Expected installer, external signing key, edition configuration and release notes.');
  const config = JSON.parse(fs.readFileSync(configPath, 'utf8'));
  const key = crypto.createPrivateKey(fs.readFileSync(keyPath));
  if (key.asymmetricKeyType !== 'ed25519') throw Error('Expected an Ed25519 key.');
  const publicKey = crypto.createPublicKey(key);
  const rawPublic = Buffer.from(publicKey.export({format: 'jwk'}).x, 'base64url');
  if (!rawPublic.equals(Buffer.from(config.UpdatePublicKey, 'base64'))) throw Error('Signing key does not match this edition.');
  const bytes = fs.readFileSync(installer);
  const version = /^radioMAT-Setup-(\d+\.\d+\.\d+)-x64\.exe$/.exec(path.basename(installer))?.[1];
  if (!version || bytes.length > 256 * 1024 * 1024) throw Error('Invalid installer name or size.');
  const signature = crypto.sign(null, bytes, key);
  if (!crypto.verify(null, bytes, publicKey, signature)) throw Error('Signature verification failed.');
  fs.writeFileSync(installer + '.podpis', signature.toString('base64') + '\n');
  fs.writeFileSync(installer + '.sha256', crypto.createHash('sha256').update(bytes).digest('hex') + '  ' + path.basename(installer) + '\n');
  const notes = fs.readFileSync(notesPath, 'utf8').trim();
  if (!notes || notes.length > 12000) throw Error('Missing or overlong release notes.');
  const manifest = {program: config.DisplayName, wersja: version, instalator: path.basename(installer), rozmiar: bytes.length,
    data: new Date().toISOString().slice(0,10), opis: notes};
  fs.writeFileSync(path.join(path.dirname(installer), 'najnowsza.json'), JSON.stringify(manifest, null, 2) + '\n');
  console.log('Signed installer, SHA256 and version description prepared. No publication.');
} catch (error) {
  // Avoid propagating library errors that could include key data.
  console.error(error.message && /^(Expected|Signing key|Signature verification|Invalid installer|Missing or overlong)/.test(error.message)
    ? error.message : 'Signing failed. Check the external key and input files. No key contents were printed.');
  process.exitCode = 1;
}
