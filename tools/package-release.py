#!/usr/bin/env python3
"""Build unsigned framework-dependent preview archives for one explicit RID."""
import argparse
import json
import os
import re
import plistlib
import shutil
import subprocess
import tarfile
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path
from installer_support import build_deb, build_msix

ROOT=Path(__file__).resolve().parents[1]


def licenses(destination):
    records={}
    directory=destination/'licenses'
    directory.mkdir(parents=True,exist_ok=True)
    for project in ('GARbro.Cli','GARbro.Desktop'):
        assets=json.loads((ROOT/'modern'/project/'obj/project.assets.json').read_text())
        for name,library in assets['libraries'].items():
            if library['type']!='package':continue
            record={'package':name,'sha512':library.get('sha512')}
            package=next((Path(base)/library['path'] for base in assets['packageFolders'] if (Path(base)/library['path']).exists()),None)
            if package:
                nuspec=next(package.glob('*.nuspec'),None)
                if nuspec:
                    xml=ET.parse(nuspec)
                    license_node=next((n for n in xml.iter() if n.tag.split('}')[-1]=='license'),None)
                    if license_node is not None:record['license']=license_node.text
                    for tag in ('authors','copyright','projectUrl'):
                        node=next((n for n in xml.iter() if n.tag.split('}')[-1]==tag),None)
                        if node is not None:record[tag]=node.text
                    target=directory/name.replace('/','-')/'package.nuspec'
                    target.parent.mkdir(parents=True,exist_ok=True)
                    shutil.copy2(nuspec,target)
                for file in package.rglob('*'):
                    if file.is_file() and ('license' in file.name.lower() or 'licence' in file.name.lower()
                        or file.name.lower().startswith(('notice','copying','copyright'))):
                        target=directory/name.replace('/','-')/file.relative_to(package)
                        target.parent.mkdir(parents=True,exist_ok=True)
                        shutil.copy2(file,target)
            records[name]=record
    (destination/'dependencies.json').write_text(json.dumps(sorted(records.values(),key=lambda r:r['package']),indent=2)+'\n')
    shutil.copy2(ROOT/'LICENSE',destination/'LICENSE')
    shutil.copy2(ROOT/'docs/third-party.md',destination/'third-party.md')
    shutil.copytree(ROOT/'docs/licenses',directory/'source-notices')
    shutil.copy2(ROOT/'docs/compatibility.md',destination/'compatibility.md')
    shutil.copytree(ROOT/'docs/compatibility',destination/'compatibility')
    shutil.copy2(ROOT/'docs/migration-status.md',destination/'migration-status.md')
    for name in ('linux-desktop.md','install.md','manual.en-US.md','manual.zh-CN.md','desktop-parity.md'):
        shutil.copy2(ROOT/'docs'/name,destination/name)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--rid',required=True,choices=['linux-x64','linux-arm64','win-x64','win-arm64','osx-x64','osx-arm64'])
    parser.add_argument('--output',default='artifacts')
    parser.add_argument('--dotnet',default='dotnet')
    parser.add_argument('--version',default=os.environ.get('GARBRO_PACKAGE_VERSION','0.2.0-preview.1'))
    parser.add_argument('--installer',action='store_true',help='Also build DEB on Linux x64 or signed test MSIX on Windows x64')
    args=parser.parse_args()
    if not re.fullmatch(r'\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?',args.version):parser.error('Invalid package version')
    output=Path(args.output).resolve();stage=output/('garbro-'+args.version+'-'+args.rid)
    if stage.exists():shutil.rmtree(stage)
    stage.mkdir(parents=True)
    for project,folder in [('GARbro.Cli','cli'),('GARbro.Desktop','desktop')]:
        subprocess.run([args.dotnet,'publish',str(ROOT/'modern'/project/(project+'.csproj')),
            '-c','Release','-r',args.rid,'--self-contained','false','-o',str(stage/folder),
            '-p:Version='+args.version,'-m:1','-nr:false','-p:UseSharedCompilation=false','--nologo','-v:q'],cwd=ROOT,check=True)
        if args.rid.startswith('linux-'):
            # Linux follows the distribution's SDL2 ABI and security updates.
            (stage/folder/'libSDL2-2.0.so').unlink(missing_ok=True)
    if args.rid.startswith('osx-'):
        desktop=stage/'desktop';app=stage/'GARbro.app/Contents';app.mkdir(parents=True)
        shutil.move(str(desktop),app/'MacOS')
        with (app/'Info.plist').open('wb')as stream:
            plistlib.dump({'CFBundleIdentifier':'org.garbro.desktop','CFBundleName':'GARbro',
                'CFBundleDisplayName':'GARbro','CFBundleExecutable':'GARbro.Desktop','CFBundlePackageType':'APPL',
                'CFBundleIconFile':'app-icon.icns',
                'CFBundleVersion':args.version.split('-')[0],'CFBundleShortVersionString':args.version.split('-')[0],'NSHighResolutionCapable':True},stream)
        resources=app/'Resources';resources.mkdir()
        shutil.copy2(ROOT/'modern/GARbro.Desktop/Assets/app-icon.icns',resources/'app-icon.icns')
    licenses(stage)
    (stage/'README.txt').write_text(f'GARbro {args.version} ({args.rid})\nRequires the .NET 10 runtime.\n'
        'This archive is unsigned. See compatibility.md for verified formats and known limits.\n'
        'Run cli/garbro for commands, or the desktop executable / macOS app bundle.\n')
    archive=output/(stage.name+('.zip'if args.rid.startswith('win-')else '.tar.gz'))
    if archive.suffix=='.zip':
        with zipfile.ZipFile(archive,'w',compression=zipfile.ZIP_DEFLATED)as z:
            for file in sorted(stage.rglob('*')):
                if file.is_file():z.write(file,file.relative_to(output))
    else:
        with tarfile.open(archive,'w:gz')as z:z.add(stage,arcname=stage.name)
    print(archive)
    if args.installer:
        if args.rid=='linux-x64': print(build_deb(stage,output,args.version))
        elif args.rid=='win-x64': print(build_msix(stage,output,args.version))
        else: parser.error('Installers are currently available for linux-x64 and win-x64')

if __name__=='__main__':main()
