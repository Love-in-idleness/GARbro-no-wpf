import importlib.util
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

spec=importlib.util.spec_from_file_location('installer_support',Path(__file__).resolve().parents[1]/'installer_support.py')
installer=importlib.util.module_from_spec(spec);spec.loader.exec_module(installer)


class InstallerTests(unittest.TestCase):
    def test_versions_preserve_prerelease_to_stable_upgrade_order(self):
        releases=['0.2.0-alpha.1','0.2.0-beta.1','0.2.0-preview.1','0.2.0-preview.10','0.2.0-rc.1','0.2.0','0.2.1-preview.1']
        windows=[tuple(map(int,installer.versions(v)[1].split('.')))for v in releases]
        self.assertEqual(windows,sorted(windows))
        self.assertEqual('0.2.0~preview.1',installer.versions(releases[2])[0])
        for version in ('../0.2.0','0.2.0-preview.10000','0.2.0\ncommand'):
            with self.subTest(version=version),self.assertRaises(ValueError):installer.versions(version)

    def test_associations_exclude_common_media_and_executable_types(self):
        extensions=installer.associations()
        for extension in ('xp3','rpa','pfs','ypf','lim'):self.assertIn(extension,extensions)
        self.assertFalse(set(extensions)&{'exe','dll','lnk','dat','bin','png','jpg','wav','mp3','zip','txt','md','lib','psb','psd','xfl','pac'})
        desktop=installer.desktop_entry(extensions)
        self.assertIn('Terminal=false\n',desktop)
        self.assertIn('Exec=garbro-desktop %F\n',desktop)

    def test_msix_uses_gui_entry_and_quoted_file_activation(self):
        manifest=installer.msix_manifest('0.2.0-preview.1')
        root=ET.fromstring(manifest)
        for prefix in root.get('IgnorableNamespaces').split():self.assertIn('xmlns:'+prefix+'=',manifest)
        ns={'m':'http://schemas.microsoft.com/appx/manifest/foundation/windows10','u3':'http://schemas.microsoft.com/appx/manifest/uap/windows10/3'}
        self.assertEqual('desktop/GARbro.Desktop.exe',root.find('.//m:Application',ns).get('Executable'))
        self.assertEqual('"%1"',root.find('.//u3:Verb',ns).get('Parameters'))
        self.assertEqual('"%1"',root.find('.//u3:FileTypeAssociation',ns).get('Parameters'))
        identity=root.find('m:Identity',ns)
        self.assertEqual(installer.PUBLISHER,identity.get('Publisher'))
