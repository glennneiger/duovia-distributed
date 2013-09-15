using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DuoVia.Net.Distributed.Agent;

namespace dpp
{
    class Program
    {
        static void Main(string[] args)
        {
            var session = Session.Create(args);
            if (null != session) session.WaitUntilExit();
        }
    }
}
