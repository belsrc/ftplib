// -------------------------------------------------------------------------------
//    FtpItem.cs
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
    using System;

    /// <summary>
    /// Class for representing a FTP item.
    /// </summary>
    public abstract class FtpItem {
        /* Fields
           ---------------------------------------------------------------------------------------*/

        private string _host;
        private string _path;

        /* Constructors
           ---------------------------------------------------------------------------------------*/

        /// <summary>
        /// Initializes a new instance of the <see cref="FtpItem"/> class.
        /// </summary>
        public FtpItem() { }

        /* Properties
           ---------------------------------------------------------------------------------------*/

        /// <summary>
        /// Gets or sets the Host for this item.
        /// </summary>
        public string Host {
            get { return this._host + ( this._host.EndsWith( "/" ) ? string.Empty : "/" ); }
            set { this._host = value; }
        }

        /// <summary>
        /// Gets or sets the file path for this item.
        /// </summary>
        public string Path {
            get {
                if( this._path == string.Empty ) {
                    // Means it is probably the root path so leave it blank
                    // since the Host already gets "/" appended
                    return string.Empty;
                }

                return this._path + ( this._path.EndsWith( "/" ) ? string.Empty : "/" );
            }

            set { this._path = value; }
        }

        /// <summary>
        /// Gets the full canonical path for this item.
        /// </summary>
        public string Canonical {
            get { return "ftp://" + Host + Path + Name; }
        }

        /// <summary>
        /// Gets or sets the permissions for this item.
        /// </summary>
        public string Permissions { get; set; }

        /// <summary>
        /// Gets or sets the file code for this item.
        /// </summary>
        public int FileCode { get; set; }

        /// <summary>
        /// Gets or sets the owner for this item.
        /// </summary>
        public string Owner { get; set; }

        /// <summary>
        /// Gets or sets the group this item belongs to.
        /// </summary>
        public string Group { get; set; }

        /// <summary>
        /// Gets or sets the size of this item.
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// Gets the formatted, readable size of this item.
        /// </summary>
        public string FormattedSize {
            get { return AutoFileSize( Size ); }
        }

        /// <summary>
        /// Gets or sets the time stamp of this item.
        /// </summary>
        public DateTime? Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the name of this item.
        /// </summary>
        public string Name { get; set; }

        /* Methods
           ---------------------------------------------------------------------------------------*/

        private string AutoFileSize( long number ) {
            double tmp = number;
            string suffix = " B ";

            if( tmp > 1024 ) { tmp = tmp / 1024; suffix = " KB"; }
            if( tmp > 1024 ) { tmp = tmp / 1024; suffix = " MB"; }
            if( tmp > 1024 ) { tmp = tmp / 1024; suffix = " GB"; }
            if( tmp > 1024 ) { tmp = tmp / 1024; suffix = " TB"; }

            return tmp.ToString( "n" ) + suffix;
        }
    }
}
