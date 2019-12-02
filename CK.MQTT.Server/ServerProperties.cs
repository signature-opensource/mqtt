using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text;

namespace CK.MQTT
{
    public static class ServerProperties
    {
        public static ResourceManager Resources
        {
            get
            {
                var a = Assembly.GetExecutingAssembly();
                Debug.Assert( a.GetManifestResourceNames().Contains( "CK.MQTT.Server.Properties.Resources.resources" ) );
                return new ResourceManager( "CK.MQTT.Server.Properties.Resources", a );
            }
        }
    }
}
