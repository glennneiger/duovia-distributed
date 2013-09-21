using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;

namespace DuoVia.Net.Distributed.HostService
{
    [RunInstaller(true)]
    public partial class DistServiceInstaller : System.Configuration.Install.Installer
    {
        public DistServiceInstaller()
        {
            InitializeComponent();
        }
    }
}
