using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Net.Mime;

namespace BasicHttpServer
{
	class BasicHttpServer
	{
		private string _prefix;
		private string _baseDir = null;
		private Dictionary<string, APIHandler> _registeredHandlers = new Dictionary<string, APIHandler>();

		private static Logger.LogFunc Log = Logger.BuildClassLogger( "BasicHttpServer" );

		/// <summary>
		/// Construct the basic http server.
		/// </summary>
		/// <param name="prefix">The base URI to use. Ex: http://localhost:80</param>
		public BasicHttpServer( string prefix ) {
			_prefix = prefix;
			if( !_prefix.EndsWith( "/" ) ) {
				_prefix += "/";
			}
		}

		/// <summary>
		/// Enable serving up static content.
		/// </summary>
		/// <param name="baseDir">The base directory the content is stored in</param>
		public void HandleFiles( string baseDir ) {
			_baseDir = baseDir;

			Log( "HandleFiles", $"Hosting static files from: { _baseDir }" );
		}

		/// <summary>
		/// Registers a handler to process requests for a specific API route.
		/// </summary>
		/// <param name="name">The name of the API.  Combined with the prefix to construct the path to the API</param>
		/// <param name="handler">The handler that will process the request.</param>
		public void RegisterAPIHandler( string name, APIHandler handler ) {
			_registeredHandlers[ name ] = handler;
		}

		public async Task Start() {
			HttpListener listener = new HttpListener();
			listener.Prefixes.Add( _prefix );
			//TODO: Add prefixes for APIs?

			listener.Start();

			while( true ) {
				await ProcessRequest( await listener.GetContextAsync() );
			}
		}

		private async Task ProcessRequest( HttpListenerContext context ) {
			Log( "ProcessRequest", "Request received: " + context.Request.Url.PathAndQuery );
			HttpListenerRequest request = context.Request;

			//TODO: Match up the first segment with an API Handler

			if( _baseDir == null ) {
				await WriteResponse( context.Response, "Server not configured to host static content", 500 );
				return;
			}

			string sanitizedPath = request.Url.LocalPath.ToLower().Trim();
			if( sanitizedPath == "/" ) {
				sanitizedPath = "/index.html";
			}
			if( sanitizedPath.EndsWith( ".htm" ) ) {
				sanitizedPath += "l";
			}

			string fullPath = _baseDir + sanitizedPath;
			Log( "ProcessRequest", "Serving up file: " + fullPath );
			if( !File.Exists( fullPath ) ) {
				await WriteResponse( context.Response, "No file found", 404 );
				return;
			}

			string contentType = "text/plain";
			switch( Path.GetExtension( fullPath ) ) {
				case ".html":
					contentType = "text/html";
					break;
				case ".css":
					contentType = "text/css";
					break;
				case ".js":
					contentType = "text/javascript";
					break;
			}

			context.Response.ContentType = contentType;

			try {
				using( FileStream fs = File.Open( fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite ) ) {
					await fs.CopyToAsync( context.Response.OutputStream );
				}
				context.Response.StatusCode = 200;
			}
			catch( Exception e ) {
				Log( "ProcessRequest", "Failed to load static file '" + fullPath + "'", e );
				await WriteResponse( context.Response, "Failed to load file", 500 );
			}
			finally {
				context.Response.Close();
				context.Response.Close();
			}
		}

		private async Task WriteResponse( HttpListenerResponse response, string message, int statusCode ) {
			response.StatusCode = statusCode;
			using( StreamWriter sw = new StreamWriter( response.OutputStream ) ) {
				await sw.WriteLineAsync( message );
			}
		}
	}
}
