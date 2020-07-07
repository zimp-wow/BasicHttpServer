using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.Threading.Tasks;
using System.IO;

namespace BasicHttpServerCore
{
	public class FileUpload
	{
		private static Logger.LogFunc Log = Logger.BuildClassLogger( "FileUpload" );

		private static Regex boundaryRegex = new Regex( ";\\s*boundary=(.*)", RegexOptions.Compiled );
		private static Regex filenameRegex = new Regex( "filename=\"(.*?)\"", RegexOptions.Compiled );

		private class FilePart {
			public Dictionary<string, string> Headers            { get; set; } = new Dictionary<string, string>();
			public bool                       HeaderFound        { get; set; } = false;
			public string                     Filename           { get; set; }
			public bool                       Complete           { get; set; } = false;
			public long                       DataStart          { get; set; }
			public long                       DataEnd            { get; set; }
			public long                       LastIndexProcessed { get; set; } = 0;
		}

		public static async Task<Dictionary<string, string>> HandleUpload( HttpListenerContext context, string baseDir = "." ) {
			HttpListenerRequest request = context.Request;
			Dictionary<string, string> retVal = new Dictionary<string, string>();

			Match m = boundaryRegex.Match( request.ContentType );
			if( !m.Success ) {
				throw new Exception( "Failed to locate boundary" );
			}

			string boundary = m.Groups[1].Value;

			Stream input = context.Request.InputStream;
			byte [] buffer = new byte[1024];
			MemoryStream partStream = new MemoryStream();
			FilePart activePart = null;
			string activeFilename = null;
			FileStream fs = null;
			try {
				while( true ) {
					int bytesRead = await input.ReadAsync( buffer, 0, buffer.Length );
					if( bytesRead == 0 ) {
						break;
					}

					partStream.Seek( 0, SeekOrigin.End );
					await partStream.WriteAsync( buffer, 0, bytesRead );

					activePart = ProcessPart( partStream, boundary, activePart );
					if( activePart.Filename != activeFilename ) {
						fs?.Close();
						string tempName = Path.Combine( baseDir, Guid.NewGuid().ToString() );
						fs = new FileStream( tempName, FileMode.Create );
						activeFilename = activePart.Filename;
						retVal[activeFilename] = tempName;
					}

					if( activePart.Complete ) {
						partStream.Seek( activePart.DataStart, SeekOrigin.Begin );
						long bytesRemaining = activePart.DataEnd - activePart.DataStart;
						while( bytesRemaining > 0 ) {
							int bytesToRead = (int)bytesRemaining;
							if( bytesToRead > buffer.Length ) {
								bytesToRead = buffer.Length;
							}

							bytesRead = partStream.Read( buffer, 0, bytesToRead );

							await fs?.WriteAsync( buffer, 0, bytesRead );

							bytesRemaining -= bytesRead;
						}

						MemoryStream newStream = new MemoryStream();
						partStream.Seek( activePart.DataEnd, SeekOrigin.Begin );
						partStream.CopyTo( newStream );
						partStream.Dispose();
						partStream = newStream;

						activePart = null;
					}
				}
			}
			finally {
				fs?.Close();
			}

			return retVal;
		}

		private static FilePart ProcessPart( MemoryStream partStream, string boundary, FilePart previousResult = null ) {
			FilePart retVal = previousResult;
			if( retVal == null ) {
				retVal = new FilePart();
			}

			long startIndex = 0;
			if( previousResult != null ) {
				startIndex = previousResult.LastIndexProcessed - boundary.Length;
				if( startIndex < 0 ) {
					startIndex = 0;
				}

			}

			partStream.Seek( startIndex, SeekOrigin.Begin );

			string readLine() {
				List<byte> strBytes = new List<byte>();
				while( partStream.Position < partStream.Length - 2 ) {
					int b = partStream.ReadByte();
					if( b == 13 ) { //CR
						b = partStream.ReadByte();
						if( b == 10 ) { //LF
							return Encoding.UTF8.GetString( strBytes.ToArray() );
						}
					}
					
					strBytes.Add( (byte)b );
				}

				return null;
			}

			byte [] boundaryBytes = Encoding.UTF8.GetBytes( "--" + boundary );

			int boundaryIndex = 0;
			while( partStream.Position < partStream.Length - 1 ) {
				int b = partStream.ReadByte();
				if( boundaryBytes[boundaryIndex] == b ) {
					boundaryIndex++;
					if( boundaryIndex >= boundaryBytes.Length ) {
						long posBeforeBoundary = partStream.Position - boundaryBytes.Length;

						string line = readLine(); //Reads off the last characters following the boundary
						if( !retVal.HeaderFound ) {
							line = readLine();
							while( line != null && line.Trim() != string.Empty ) {
								string[] comps = line.Split( ":" );
								if( comps.Length == 2 ) {
									retVal.Headers[ comps[0].Trim() ] = comps[1].Trim();
								}

								line = readLine();
							}

							if( retVal.Headers.TryGetValue( "Content-Disposition", out string disposition ) ) {
								Match m = filenameRegex.Match( disposition );
								if( m.Success ) {
									retVal.Filename = m.Groups[1].Value;
								}
							}

							if( line != null && line.Trim() == string.Empty ) {
								retVal.HeaderFound = true;
								retVal.DataStart = partStream.Position;
								boundaryBytes = Encoding.UTF8.GetBytes( "\r\n--" + boundary + "--" );
							}
						}
						else {
							retVal.DataEnd = posBeforeBoundary;
							if( line != null ) {
								retVal.DataEnd -= line.Length;
							}

							retVal.Complete = true;
						}

						boundaryIndex = 0;
					}
				}
				else {
					boundaryIndex = 0;
				}
			}

			retVal.LastIndexProcessed = partStream.Position;

			return retVal;
		}
	}
}
