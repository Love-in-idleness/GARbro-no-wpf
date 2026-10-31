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
    defaults=Path.home()/'.config/mimeapps.list';defaults.parent.mkdir(parents=True,exist_ok=True)
    original='[Default Applications]\napplication/x-existing-xp3=existing-application.desktop\n'
    defaults.write_text(original)
    Path('/usr/share/mime/packages/existing-test.xml').write_text('<mime-info xmlns="http://www.freedesktop.org/standards/shared-mime-info"><mime-type type="application/x-existing-xp3"><glob pattern="*.xp3"/></mime-type></mime-info>')
    Path('/usr/share/applications/existing-application.desktop').write_text('[Desktop Entry]\nType=Application\nName=Existing\nExec=/usr/bin/true\n')
    run('update-mime-database','/usr/share/mime',stdout=subprocess.DEVNULL)
    Path('/tmp/associated.xp3').write_bytes(b'fixture')
    with tempfile.TemporaryDirectory()as directory:
        previous=Path(directory)/'previous'
        run('dpkg-deb','--raw-extract','/tmp/garbro.deb',str(previous),stdout=subprocess.DEVNULL)
        control=previous/'DEBIAN/control'
        version=subprocess.check_output(['dpkg-deb','-f','/tmp/garbro.deb','Version'],text=True).strip()
        control.write_text(control.read_text().replace('Version: '+version+'\n','Version: '+version+'~installer-test\n'))
        older=Path(directory)/'previous.deb'
        run('dpkg-deb','--build',str(previous),str(older),stdout=subprocess.DEVNULL)
        run('apt-get','install','-y','--allow-downgrades',str(older),stdout=subprocess.DEVNULL)
        run('apt-get','install','-y','/tmp/garbro.deb',stdout=subprocess.DEVNULL)
        assert subprocess.check_output(['dpkg-query','-W','-f=${Version}','garbro'],text=True)==version
        assert defaults.read_text()==original
        assert subprocess.check_output(['xdg-mime','query','filetype','/tmp/associated.xp3'],text=True).strip()=='application/x-existing-xp3'
        assert subprocess.check_output(['xdg-mime','query','default','application/x-existing-xp3'],text=True).strip()=='existing-application.desktop'
    output=json.loads(subprocess.check_output(['garbro','list','/tmp/standard.xfl','--json'],text=True))
    assert output[0]['format']=='XFL'and len(output[0]['entries'])==3
    with tempfile.TemporaryDirectory()as directory:
        run('garbro','extract','/tmp/standard.xfl','-o',str(Path(directory)/'output'))
        assert (Path(directory)/'output/hello.txt').read_text()=='Hello, GARbro!\n'
    run('xvfb-run','-a','python3','/probe.py','--gui')
    run('apt-get','remove','-y','garbro',stdout=subprocess.DEVNULL)
    assert not Path('/usr/share/applications/org.garbro.GARbro.desktop').exists()
    assert not Path('/opt/garbro/desktop/GARbro.Desktop').exists()
    assert defaults.read_text()==original
    print('DEB install, upgrade, default preservation, CLI, desktop activation and uninstall passed.')
