'use strict';
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '..');
const target = path.join(root, '.build', 'public-source');
// Explicit allowlist: configuration, keys, build output and working notes stay local.
const names = ['Assets', 'Branding', 'Data', 'docs', 'installer', 'tools', '.gitattributes', '.gitignore',
  'Radiomat.csproj', 'NuGet.Config', 'README.md', 'LICENSE', 'RELEASE-NOTES.md', 'SPEC.md'];
names.push(...fs.readdirSync(root).filter(n => /\.(cs|xaml)$/.test(n)));
if (!target.startsWith(root + path.sep) || fs.lstatSync(root).isSymbolicLink()) throw Error('Invalid workspace.');
if (fs.existsSync(target)) {
  if (fs.lstatSync(target).isSymbolicLink()) throw Error('Invalid snapshot directory.');
  fs.rmSync(target, {recursive: true});
}
fs.mkdirSync(target, {recursive: true});
for (const name of names) fs.cpSync(path.join(root, name), path.join(target, name), {
  recursive: true, filter: p => !fs.lstatSync(p).isSymbolicLink() && !p.endsWith('.local.txt')
});
const publicReadme = path.join(root, 'Branding', 'Public', 'README.md');
if (fs.existsSync(publicReadme)) fs.copyFileSync(publicReadme, path.join(target, 'README.md'));
console.log('Prepared source snapshot without local configuration, private keys or working notes.');
