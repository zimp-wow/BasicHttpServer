using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BasicHttpServerCore
{
    public class Logger
    {
        private static Action<string> _handler = null;

        public static void Init( Action<string> handler ) {
            _handler = handler;
        }

        public delegate void LogFunc( string context, string message, Exception e = null );

        public static void Log( string context, string message, Exception e = null ) {
            if( _handler == null ) {
                return;
            }

            string line = $"{ context } - { message }";
            if( e != null ) {
                line = $"{ line } - { e.Message }\n{ e }";
            }

            _handler.Invoke( line );
        }

        public static LogFunc BuildClassLogger( string className ) {
            void retVal( string context, string message, Exception e = null ) {
                Log( $"{ className }:{ context}", message, e );
            }

            return retVal;
        }
    }
}
