#!/usr/bin/env python3
"""Exercise the built CLI's public commands, JSON and exit codes."""
import argparse
import json
import subprocess
import tempfile
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--dotnet',default='dotnet');args=parser.parse_args()
CLI=ROOT/'modern/GARbro.Cli/bin/Release/net10.0/garbro.dll'
FIX=ROOT/'tests/fixtures/generated'
def run(*arguments,code=0):
 result=subprocess.run([args.dotnet,str(CLI),*map(str,arguments),'--json'],capture_output=True,text=True,cwd=ROOT)
 if result.returncode!=code:raise AssertionError((arguments,result.returncode,result.stdout,result.stderr))
 return json.loads(result.stdout)
assert any(f['tag']=='XP3'for f in run('formats'))
assert run('probe',FIX/'standard.xp3','--scheme','none')[0]['format']=='XP3'
assert run('probe',FIX/'raw565.lim')[0]['format']=='LIM'
assert len(run('list',FIX/'pf8.pfs')[0]['entries'])==3
with tempfile.TemporaryDirectory(prefix='garbro-cli-')as folder:
 root=Path(folder)
 run('extract',FIX/'standard.xp3','--scheme','none','-o',root/'output')
 assert (root/'output/hello.txt').read_text()=='Hello, GARbro!\n'
 run('convert',FIX/'raw565.lim','-o',root/'image.png')
 assert (root/'image.png').read_bytes().startswith(b'\x89PNG\r\n\x1a\n')
 run('extract',FIX/'unsafe.xfl','-o',root/'unsafe',code=3)
 assert not (root/'escape.txt').exists()
 run('extract',FIX/'standard.xp3','-o',root/'missing-key',code=4)
print('CLI smoke checks passed')
