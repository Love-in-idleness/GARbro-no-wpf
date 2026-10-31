"""Executed only in the disposable Debian/Ubuntu installer test container."""
import json
import os
import subprocess
import tempfile
import time
from pathlib import Path


def run(*args,**kwargs):return subprocess.run(args,check=True,**kwargs)


def gui():
    # GIO uses the installed desktop entry, including its file activation args.
    run('gio','launch','/usr/share/applications/org.garbro.GARbro.desktop','/tmp/standard.xfl')
    deadline=time.monotonic()+20
    while time.monotonic()<deadline:
        windows=subprocess.check_output(['xwininfo','-root','-tree'],text=True)
        if 'GARbro — standard.xfl'in windows:
            print('Installed desktop entry opened the associated archive without a terminal.')
            return
        time.sleep(.2)
    raise AssertionError('Installed desktop did not open the archive')


if __name__=='__main__':
    if '--gui'in __import__('sys').argv:gui();raise SystemExit(0)
    desktop=Path('/usr/share/applications/org.garbro.GARbro.desktop').read_text()
    assert 'Terminal=false'in desktop
    assert Path('/usr/share/icons/hicolor/256x256/apps/org.garbro.GARbro.png').is_file()
    output=json.loads(subprocess.check_output(['garbro','list','/tmp/standard.xfl','--json'],text=True))
    assert output[0]['format']=='XFL'and len(output[0]['entries'])==3
    with tempfile.TemporaryDirectory()as directory:
        run('garbro','extract','/tmp/standard.xfl','-o',str(Path(directory)/'output'))
        assert (Path(directory)/'output/hello.txt').read_text()=='Hello, GARbro!\n'
    run('xvfb-run','-a','python3','/probe.py','--gui')
    run('apt-get','remove','-y','garbro',stdout=subprocess.DEVNULL)
    assert not Path('/usr/share/applications/org.garbro.GARbro.desktop').exists()
    assert not Path('/opt/garbro/desktop/GARbro.Desktop').exists()
    print('DEB install, CLI, desktop activation and uninstall passed.')
