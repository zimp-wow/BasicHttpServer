using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BasicHttpServerCore
{
	public class APIUtil
	{
		public static async Task<T> DeserializeRequest<T>( HttpListenerRequest request ) {
			StreamReader sr = new StreamReader( request.InputStream );
			string json = await sr.ReadToEndAsync();

			return JsonConvert.DeserializeObject<T>( json );
		}

		public static async Task JsonResponse<T>( T respObj, HttpListenerResponse response ) {
			string json = JsonConvert.SerializeObject( respObj );
			byte[] bytes = System.Text.Encoding.UTF8.GetBytes( json );

			response.ContentType = "application/json";
			response.ContentLength64 = bytes.Length;

			await response.OutputStream.WriteAsync( bytes, 0, bytes.Length );
		}
	}
}
