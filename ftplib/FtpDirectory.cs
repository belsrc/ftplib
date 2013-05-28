// -------------------------------------------------------------------------------
//    FtpDirectory.cs
//    Copyright (c) 2013 Bryan Kizer
//    All rights reserved.
//
//    Redistribution and use in source and binary forms, with or without
//    modification, are permitted provided that the following conditions are
//    met:
//
//    Redistributions of source code must retain the above copyright notice,
//    this list of conditions and the following disclaimer.
//
//    Redistributions in binary form must reproduce the above copyright notice,
//    this list of conditions and the following disclaimer in the documentation
//    and/or other materials provided with the distribution.
//
//    Neither the name of the Organization nor the names of its contributors
//    may be used to endorse or promote products derived from this software
//    without specific prior written permission.
//
//    THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS
//    IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
//    TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A
//    PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
//    HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
//    SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED
//    TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
//    PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
//    LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
//    NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
//    SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// -------------------------------------------------------------------------------
namespace FtpLib {
    /// <summary>
    /// Class for representing a FTP directory.
    /// </summary>
    public class FtpDirectory : FtpItem {
        /// <summary>
        /// Initializes a new instance of the FtpDirectory class.
        /// </summary>
        public FtpDirectory() : base() { }

        /// <summary>
        /// Gets the parent directory.
        /// </summary>
        public string ParentDirectory {
            get {
                // Means it is currently in root
                if( Path == string.Empty ) {
                    return string.Empty;
                }

                // Since the Path always has a trailing '/' we need to get rid of it so the 
                // last element is not an empty string
                var tmp = Path.Substring( 0, Path.LastIndexOf( '/' ) ).Split( new char[] { '/' } );

                if( tmp[ tmp.Length - 1 ] == string.Empty ) {
                    return "/";
                }

                return tmp[ tmp.Length - 1 ];
            }
        }
    }
}
