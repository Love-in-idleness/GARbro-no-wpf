#!/usr/bin/env python3
"""Unpack and exercise a generated preview package on its target platform."""
import argparse
import gzip
import hashlib
import json
import os
import subprocess
import sys
import tarfile
import tempfile
import zipfile
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--rid',required=True)
parser.add_argument('--output',type=Path,default=ROOT/'artifacts')
parser.add_argument('--dotnet',default='dotnet')
parser.add_argument('--version',default=os.environ.get('GARBRO_PACKAGE_VERSION','0.1.0-migration'))
args=parser.parse_args()
name='garbro-'+args.version+'-'+args.rid
archive=args.output/(name+('.zip'if args.rid.startswith('win-')else'.tar.gz'))
with tempfile.TemporaryDirectory(prefix='garbro-package-')as folder:
    root=Path(folder)
    if archive.suffix=='.zip':
        with zipfile.ZipFile(archive)as source:
            for item in source.namelist():
                if not (root/item).resolve().is_relative_to(root.resolve()):
                    raise ValueError('Archive contains an escaping path')
            source.extractall(root)
    else:
        with tarfile.open(archive)as source:
            source.extractall(root,filter='data')
    package=root/name
    for relative in ('LICENSE','dependencies.json','licenses','compatibility.md',
                     'compatibility/baseline-source.json','compatibility/baseline-runtime.json',
                     'compatibility/portable-status.json','migration-status.md'):
        assert (package/relative).exists(),relative
    cli=package/'cli/garbro.dll'
    desktop=package/('GARbro.app/Contents/MacOS'if args.rid.startswith('osx-')else'desktop')
    assert (desktop/'GARbro.Desktop.dll').exists()
    database=cli.parent/'GameData/Formats.json.gz'
    assert database.read_bytes()==(desktop/'GameData/Formats.json.gz').read_bytes()
    schemes=json.loads(gzip.decompress(database.read_bytes()))
    metadata=json.loads(database.with_name('schemes.json').read_text())
    assert database.with_name('schemes.json').read_bytes()==(desktop/'GameData/schemes.json').read_bytes()
    assert schemes['Version']==metadata['version']==148 and schemes['PortableSchemaVersion']==2
    assert sorted(schemes['SchemeMap'])==metadata['applied']
    assert hashlib.sha256(gzip.decompress(database.read_bytes())).hexdigest()==metadata['json_sha256']
    assert json.loads((package/'dependencies.json').read_text())
    subprocess.run([sys.executable,str(ROOT/'tools/smoke-cli.py'),
                    '--dotnet',args.dotnet,'--cli',str(cli)],check=True,cwd=root)
print('Unpacked package smoke checks passed:',args.rid)
