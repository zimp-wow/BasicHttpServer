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
		private static Logger.LogFunc Log = Logger.BuildClassLogger( "TestAPI" );

		private class TestObj {
			public string RequestArg { get; set; }
		}

		[APIHandler( "GET", "test" )]
		public async Task Test( HttpListenerContext context ) {
			TestObj reqObj = await APIUtil.DeserializeRequest<TestObj>( context.Request );
			Log( "Test", "API invoked with argument: " + reqObj.RequestArg );

			object respObj = new {
				Test = "test"
			};
			context.Response.StatusCode = 200;
			await APIUtil.JsonResponse( respObj, context.Response );
		}
	}
}
