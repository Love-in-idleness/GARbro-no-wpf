#!/usr/bin/env python3
"""Exercise the native Avalonia window; Linux requires a display or xvfb-run."""
import argparse
import subprocess
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--dotnet',default='dotnet')
parser.add_argument('--output',type=Path,default=ROOT/'artifacts/native-desktop')
args=parser.parse_args()
subprocess.run([args.dotnet,str(ROOT/'tests/GARbro.Desktop.NativeSmoke/bin/Release/net10.0/GARbro.Desktop.NativeSmoke.dll'),
                str(ROOT/'tests/fixtures/generated/standard.xfl'),str(args.output.resolve())],check=True,timeout=45)
