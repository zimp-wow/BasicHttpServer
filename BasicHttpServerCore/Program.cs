﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;

namespace BasicHttpServerCore
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Logger.Init( ( line ) => Console.WriteLine( line ) );

            string baseDir = Environment.CurrentDirectory;
            #if DEBUG
                baseDir += "\\..\\..\\";
            #endif
            baseDir = Path.Combine( baseDir, "wwwroot" );

            BasicHttpServer server = new BasicHttpServer( "http://localhost:8080" );
            server.HandleFiles( baseDir );
            server.RegisterAPIHandler( "test", new TestAPI() );

            await server.Start();
        }
    }
}
