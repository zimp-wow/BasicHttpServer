using Microsoft.SqlServer.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace BasicHttpServer
{
    [System.AttributeUsage( System.AttributeTargets.Method )]
    class APIHandlerAttribute : System.Attribute
    {
        public string Method;
        public string Name;

        public APIHandlerAttribute( string method, string name ) {
            Method = method;
            Name   = name;
        }
    }
}
