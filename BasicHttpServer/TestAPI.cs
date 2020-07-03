using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace BasicHttpServer
{
	class TestAPI
	{
		[APIHandler( "GET", "test" )]
		public async Task Test( HttpListenerContext context ) {
			using( StreamWriter sw = new StreamWriter( context.Response.OutputStream ) ) {
				await sw.WriteLineAsync( "Test API" );
				context.Response.StatusCode = 200;
			}
		}
	}
}
