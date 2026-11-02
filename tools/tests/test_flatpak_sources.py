import importlib.util
import unittest
from pathlib import Path

spec=importlib.util.spec_from_file_location('prepare_flathub',Path(__file__).resolve().parents[1]/'prepare-flathub.py')
module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)


class FlatpakSourceTests(unittest.TestCase):
    def test_offline_sources_include_sdk_download_only_packs(self):
        assets={'libraries':{'Example/1.2.3':{'type':'package','path':'example/1.2.3'},'Project/1.0':{'type':'project'}},
                'project':{'frameworks':{'net10.0':{'downloadDependencies':[
                    {'name':'Microsoft.NETCore.App.Runtime.linux-arm64','version':'[10.0.13, 10.0.13]'},
                    {'name':'Microsoft.NETCore.App.Host.linux-arm64','version':'[10.0.13, 10.0.13]'}]}}}}
        packages=dict(module.package_records(assets))
        self.assertEqual({'example/1.2.3','microsoft.netcore.app.runtime.linux-arm64/10.0.13','microsoft.netcore.app.host.linux-arm64/10.0.13'},set(packages))

    def test_unpinned_sdk_download_is_rejected(self):
        assets={'libraries':{},'project':{'frameworks':{'net10.0':{'downloadDependencies':[
            {'name':'Microsoft.NETCore.App.Runtime.linux-arm64','version':'[10.0.13, 11.0.0)'}]}}}}
        with self.assertRaises(ValueError):module.package_records(assets)
