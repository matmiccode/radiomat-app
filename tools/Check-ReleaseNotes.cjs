'use strict';
// Each edition has its own "Co nowego". The internal description must not mention the public
// edition, author support, coffee or "two editions"; the public one must not mention the internal
// edition. Usage: node Check-ReleaseNotes.cjs <Company|Public> <notes file>
const fs = require('node:fs');
const [edition, notesPath] = process.argv.slice(2);
// Lookarounds instead of \b, because \b in JavaScript does not treat Polish letters as word letters.
const word = (body) => new RegExp('(?<![\\p{L}\\p{N}])(?:' + body + ')', 'iu');
const otherEditions = word('(?:dwa|dwóch|dwoch|oba|obu|obydwa|drugi\\p{L}*|osobn\\p{L}*|niezależn\\p{L}*|niezalezn\\p{L}*|rozdzielon\\p{L}*)\\s+(?:\\p{L}+\\s+)?wyda\\p{L}*');
const rules = {
  Company: [
    ['wydanie publiczne', word('publiczn|public')],
    ['wsparcie autora', word('wsparci|wesprz|support|autor')],
    ['kawa', word('kaw(?:a|ę|e|y|ie|ą|ce)?(?![\\p{L}])|coffee|buycoffee')],
    ['marka MATCODE', word('matcode|matmiccode|github')],
    ['angielski interfejs', word('angielsk|english')],
    ['dwa wydania', otherEditions],
    ['wydania', word('editions?(?![\\p{L}])')],
  ],
  Public: [
    ['wydanie wewnętrzne/firmowe', word('firmow|company|wewnętrzn\\p{L}*\\s+wyda|wewnetrzn\\p{L}*\\s+wyda|internal edition')],
    // "W wydaniu publicznym: …" in the public notes implies there is another edition.
    ['„wydanie publiczne”', word('wyda\\p{L}*\\s+publiczn|publiczn\\p{L}*\\s+wyda|public\\s+(?:edition|interface)')],
    ['dwa wydania', otherEditions],
    ['wydania', word('(?:separate|two|both|other|independent)\\s+editions?(?![\\p{L}])')],
  ],
};
try {
  if (!rules[edition] || !notesPath) throw Error('Usage: Check-ReleaseNotes.cjs <Company|Public> <notes file>');
  if (!fs.existsSync(notesPath)) throw Error(`Missing release notes for ${edition}: ${notesPath}`);
  const text = fs.readFileSync(notesPath, 'utf8');
  // A prepared najnowsza.json keeps the description in "opis" with escaped line breaks.
  const notes = (notesPath.toLowerCase().endsWith('.json') ? String(JSON.parse(text).opis ?? '') : text).trim();
  if (!notes || notes.length > 12000) throw Error('Missing or overlong release notes.');
  const hits = [];
  notes.split(/\r?\n/).forEach((line, index) => {
    for (const [label, pattern] of rules[edition]) {
      if (pattern.test(line)) hits.push(`  wiersz ${index + 1} (${label}): ${line.trim()}`);
    }
  });
  if (hits.length) throw Error(`Release notes for ${edition} mention another edition or its branding:\n${hits.join('\n')}`);
  console.log(`Release notes for ${edition}: OK`);
} catch (error) {
  console.error(error.message);
  process.exitCode = 1;
}
