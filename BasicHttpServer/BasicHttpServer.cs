using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Net.Mime;
using System.Net.Http;
using System.Reflection;

namespace BasicHttpServer
{
	class BasicHttpServer
	{
		private string _prefix;
		private string _baseDir = null;
		private Dictionary<string, HandlerMapping> _registeredHandlers = new Dictionary<string, HandlerMapping>();

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
		public void RegisterAPIHandler( string name, object handlerClass ) {
			if( !name.EndsWith( "/" ) ) {
				name += "/";
			}

			HandlerMapping mapping = new HandlerMapping( name, handlerClass );
			if( _registeredHandlers.ContainsKey( name ) ) {
				mapping = _registeredHandlers[ name ];
			}

			MethodInfo[] methods = handlerClass.GetType().GetMethods();
			Type attType = typeof( APIHandlerAttribute );
			foreach( MethodInfo method in methods ) {
				APIHandlerAttribute att = method.GetCustomAttribute( attType ) as APIHandlerAttribute;
				if( att == null ) {
					continue;
				}

				mapping.AddMapping( att.Method, att.Name, method );
			}

			_registeredHandlers[ name ] = mapping;
		}

		public async Task Start() {
			HttpListener listener = new HttpListener();
			listener.Prefixes.Add( _prefix );

			listener.Start();

			while( true ) {
				await ProcessRequest( await listener.GetContextAsync() );
			}
		}

		private async Task ProcessRequest( HttpListenerContext context ) {
			Log( "ProcessRequest", "Request received: " + context.Request.Url.PathAndQuery );
			HttpListenerRequest request = context.Request;

			if( request.Url.Segments.Length > 2 ) {
				string apiSegment = request.Url.Segments[1];
				if( _registeredHandlers.TryGetValue( apiSegment, out HandlerMapping mapping ) ) {
					mapping.Invoke( context );
					return;
				}
			}

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
			}
		}

		private static async Task WriteResponse( HttpListenerResponse response, string message, int statusCode ) {
			response.StatusCode = statusCode;
			using( StreamWriter sw = new StreamWriter( response.OutputStream ) ) {
				await sw.WriteLineAsync( message );
			}
		}

		private class HandlerMapping {
			private string Name;
			private object TargetInstance;
			private Dictionary<string, MethodInfo> mappedHandlers = new Dictionary<string, MethodInfo>();

			private static Logger.LogFunc Log = Logger.BuildClassLogger( "HandlerMapping" );

			public HandlerMapping( string name, object instance ) {
				Name           = name;
				TargetInstance = instance;
			}

			public void AddMapping( string method, string apiPath, MethodInfo targetMethod ) {
				mappedHandlers[ConstructKey( method, apiPath )] = targetMethod;
			}

			private string ConstructKey( string method, string apiPath ) {
				if( !apiPath.EndsWith( "/" ) ) {
					apiPath += "/";
				}

				return $"{ method.ToLower() }_{apiPath}";
			}

			public async void Invoke( HttpListenerContext context ) {
				string method = context.Request.HttpMethod;
				string apiPath = null;
				bool nextOne = false;
				foreach( string segment in context.Request.Url.Segments ) {
					if( nextOne ) {
						apiPath = segment;
						break;
					}
					if( segment == Name ) {
						nextOne = true;
					}
				}

				if( apiPath == null ) {
					Log( "Invoke", $"Failed to discover API name for request: { context.Request.Url.PathAndQuery }" );
					await WriteResponse( context.Response, "Could not determine API name", 400 );
					return;
				}

				if( mappedHandlers.TryGetValue( ConstructKey( method, apiPath ), out MethodInfo targetMethod ) ) {
					try {
						object retVal = targetMethod.Invoke( TargetInstance, new object[] { context } );
						if( retVal is Task task ) {
							await task;
						}
					}
					catch( Exception e ) {
						Log( "Invoke", "Method invocation failed", e );
						await WriteResponse( context.Response, "Failed to invoke API", 500 );
					}

					return;
				}

				await WriteResponse( context.Response, "API method not found", 404 );
			}
		}
	}
}
