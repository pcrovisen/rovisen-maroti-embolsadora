// Checks that every .cs file on disk is listed in its old-style .csproj, and
// that nothing listed is missing from disk.
//
// The shadow projects in build/typecheck/ glob their sources, but the real
// projects enumerate every file. So adding or deleting a .cs compiles cleanly
// on macOS and then fails on the Windows build — which has now happened twice
// in this branch. This closes that gap locally.

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.join(path.dirname(fileURLToPath(import.meta.url)), '..');
const projects = [
  ['ModbusServer', 'ModbusServer/ModbusServer.csproj'],
  ['TcpHMIClient', 'TcpHMIClient/TcpHMIClient.csproj'],
];

const walk = (dir) => fs.readdirSync(dir, { withFileTypes: true }).flatMap((e) => {
  if (e.name === 'obj' || e.name === 'bin') return [];
  const full = path.join(dir, e.name);
  return e.isDirectory() ? walk(full) : full.endsWith('.cs') ? [full] : [];
});

let bad = 0;
for (const [dir, proj] of projects) {
  const xml = fs.readFileSync(path.join(root, proj), 'utf8');
  // Compared case-insensitively: MSBuild on Windows resolves these off a
  // case-insensitive filesystem, so HMIForm.designer.cs and HMIForm.Designer.cs
  // are the same file there and must not be reported here.
  const listed = new Map(
    [...xml.matchAll(/<Compile\s+Include="([^"]+\.cs)"/g)]
      .map((m) => m[1].replace(/\\/g, '/'))
      .map((f) => [f.toLowerCase(), f]));

  const onDisk = walk(path.join(root, dir))
    .map((f) => path.relative(path.join(root, dir), f).split(path.sep).join('/'));

  const diskLower = new Set(onDisk.map((f) => f.toLowerCase()));

  for (const f of onDisk) {
    if (!listed.has(f.toLowerCase())) { console.error(`${proj}: on disk but not listed: ${f}`); bad++; }
  }
  for (const f of listed.values()) {
    if (!diskLower.has(f.toLowerCase())) { console.error(`${proj}: listed but not on disk: ${f}`); bad++; }
  }
}

if (bad) {
  console.error(`\n${bad} problem(s). The Windows build will fail — fix the <Compile Include> list.`);
  process.exit(1);
}
console.log('csproj source lists match the files on disk');
