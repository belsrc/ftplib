// -------------------------------------------------------------------------------
//    FtpConnection.cs
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
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Provides a FTP connection and basic operations.
    /// </summary>
    public class FtpConnection : IDisposable {
        /* Fields
           ---------------------------------------------------------------------------------------*/

        private const int DEFAULT_PORT = 21;
        private FtpWebRequest _request = null;
        private FtpWebResponse _response = null;
        private Stream _stream = null;
        private int _bufferSize = 2048;
        private bool _disposed;
        private string _host;

        // Last is Windows 'DOS' style
        private string[] _regEx = new string[] {
            "(?<dir>[\\-ld])(?<permission>([\\-r][\\-w][\\-xs]){3})\\s+(?<filecode>\\d+)\\s+(?<owner>\\w+)\\s+(?<group>\\w+)\\s+(?<size>\\d+)\\s+(?<timestamp>((?<month>\\w{3})\\s+(?<day>\\d{1,2})\\s+(?<hour>\\d{1,2}):(?<minute>\\d{2}))|((?<month>\\w{3})\\s+(?<day>\\d{1,2})\\s+(?<year>\\d{4})))\\s+(?<name>.+)",
            "(?<timestamp>(?<month>\\d{2})\\-(?<day>\\d{2})\\-(?<year>\\d{2})\\s+(?<hour>\\d{2}):(?<minute>\\d{2})(?<noon>[Aa|Pp][mM]))\\s+(?<dir>\\<\\w+\\>){0,1}\\s*(?<size>\\d+){0,1}\\s+(?<name>.+)"
        };

        /* Constructors
           ---------------------------------------------------------------------------------------*/

        /// <summary>
        /// Initializes a new instance of the <see cref="FtpConnection"/> class.
        /// </summary>
        /// <param name="host">The server name or IP address.</param>
        public FtpConnection( string host )
            : this( host, DEFAULT_PORT, string.Empty, string.Empty, false ) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="FtpConnection"/> class.
        /// </summary>
        /// <param name="host">The server name or IP address.</param>
        /// <param name="port">The server port number to connect to.</param>
        public FtpConnection( string host, int port )
            : this( host, port, string.Empty, string.Empty, false ) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="FtpConnection"/> class.
        /// </summary>
        /// <param name="host">The server name or IP address.</param>
        /// <param name="port">The server port number to connect to.</param>
        /// <param name="username">The username to use when authenticating.</param>
        /// <param name="password">The password to use when authenticating.</param>
        public FtpConnection( string host, int port, string username, string password )
            : this( host, port, username, password, false ) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="FtpConnection"/> class.
        /// </summary>
        /// <param name="host">The server name or IP address.</param>
        /// <param name="port">The server port number to connect to.</param>
        /// <param name="username">The username to use when authenticating.</param>
        /// <param name="password">The password to use when authenticating.</param>
        /// <param name="isSsl">Whether or not the connection should use SSL.</param>
        /// <exception cref="ArgumentException">
        /// The <paramref name="host"/> argument is empty.
        /// </exception>
        public FtpConnection( string host, int port, string username, string password, bool isSsl ) {
            if( string.IsNullOrWhiteSpace( host ) ) {
                throw new ArgumentException( "The supplied host can not be empty.", "host" );
            }

            this._disposed = false;
            Host = host;
            Port = port;
            Username = username;
            Password = password;
            IsSsl = isSsl;

            // These statements are to ignore certification validation warning
            ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback( OnValidateCertificate );
            ServicePointManager.Expect100Continue = true;
        }

        /* Properties
           ---------------------------------------------------------------------------------------*/

        /// <summary>
        /// Gets or sets the host address for the connection.
        /// </summary>
        public string Host {
            get { return this._host + ( this._host.EndsWith( "/" ) ? string.Empty : "/" ); }
            set { this._host = value; }
        }

        /// <summary>
        /// Gets or sets the port for the connection.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Gets or sets the username to use in authentication.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Gets or sets the password to use in authentication.
        /// </summary>
        public string Password { private get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether or not to use SSL.
        /// </summary>
        public bool IsSsl { get; set; }

        /* Methods
           ---------------------------------------------------------------------------------------*/

        /// <summary>
        /// Checks whether the file exists on the server or not.
        /// </summary>
        /// <param name="remoteFilePath">The path to the remote file [minus the the host].</param>
        /// <param name="type">The type of item that is being checked.</param>
        /// <returns><c>true</c> if the file exists, otherwise, <c>false</c>.</returns>
        public bool FtpItemExists( string remoteFilePath, FtpItemType type ) {
            // If it is an empty path were going to assume it means root
            // which is always there
            if( remoteFilePath == string.Empty ) {
                return true;
            }

            this._request = GetNewRequest( Host + remoteFilePath );

            if( type == FtpItemType.File ) {
                this._request.Method = WebRequestMethods.Ftp.GetFileSize;
            }
            else {
                this._request.Method = WebRequestMethods.Ftp.ListDirectory;
            }

            // Exception handling for control flow is ugly, unfortunately WebResponse
            // doesn't offer a better alternative for checking if a file exists.
            try {
                this._response = ( FtpWebResponse )this._request.GetResponse();
                return true;
            }
            catch( WebException ex ) {
                return false;
            }
            finally {
                if( this._response != null ) { this._response.Close(); }
                this._request = null;
            }
        }

        /// <summary>
        /// Checks whether the file exists on the server or not.
        /// </summary>
        /// <param name="item">The <see cref="FtpFile"/> to check.</param>
        /// <returns><c>true</c> if the file exists, otherwise, <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">
        /// The <paramref name="item"/> argument is null.
        /// </exception>
        public bool FtpItemExists( FtpItem item ) {
            if( item == null ) {
                throw new ArgumentNullException( "item", "The FtpItem can not be null" );
            }

            if( item.GetType() == typeof( FtpFile ) ) {
                return FtpItemExists( item.Path + item.Name, FtpItemType.File );
            }
            else {
                return FtpItemExists( item.Path + item.Name, FtpItemType.Folder );
            }
        }

        /// <summary>
        /// Downloads a file from the server.
        /// </summary>
        /// <param name="remotePath">The path to the remote file [minus the the host].</param>
        /// <param name="localFilePath">The path to the local download file.</param>
        /// <exception cref="FileNotFoundException">
        /// The <paramref name="remotePath"/> file does not exist.
        /// </exception>
        public void DownloadFile( string remotePath, string localFilePath ) {
            if( !FtpItemExists( remotePath, FtpItemType.File ) ) {
                throw new FileNotFoundException( "The file does not exist", Host + remotePath );
            }

            FileStream localStream = new FileStream( localFilePath, FileMode.Create );
            this._request = GetNewRequest( Host + remotePath );
            this._request.Method = WebRequestMethods.Ftp.DownloadFile;
            byte[] byteBuffer = new byte[ this._bufferSize ];

            try {
                this._response = ( FtpWebResponse )this._request.GetResponse();
                this._stream = this._response.GetResponseStream();
                int bytesRead = this._stream.Read( byteBuffer, 0, this._bufferSize );
                while( bytesRead > 0 ) {
                    localStream.Write( byteBuffer, 0, bytesRead );
                    bytesRead = this._stream.Read( byteBuffer, 0, this._bufferSize );
                }
            }
            catch( Exception e ) {
                throw;
            }
            finally {
                if( localStream != null ) { localStream.Close(); }
                if( this._stream != null ) { this._stream.Close(); }
                if( this._response != null ) { this._response.Close(); }
                this._request = null;
            }
        }

        /// <summary>
        /// Downloads a file from the server.
        /// </summary>
        /// <param name="file">The <see cref="FtpFile"/> to download.</param>
        /// <param name="localFilePath">The path to the local download file.</param>
        /// <exception cref="ArgumentNullException">
        /// The <paramref name="file"/> argument is null.
        /// </exception>
        /// <exception cref="UriFormatException">
        /// The <paramref name="file"/> name property is empty.
        /// </exception>
        public void DownloadFile( FtpFile file, string localFilePath ) {
            if( file == null ) {
                throw new ArgumentNullException( "file", "The FtpFile can not be null" );
            }

            if( string.IsNullOrWhiteSpace( file.Name ) ) {
                throw new UriFormatException( "The FtpFile's Name property can not be empty" );
            }

            DownloadFile( file.Path + file.Name, localFilePath );
        }

        /// <summary>
        /// Uploads a local file to the server.
        /// </summary>
        /// <param name="remotePath">The path to the remote file [minus the the host].</param>
        /// <param name="localFilePath">The path to the local file.</param>
        /// <exception cref="FileNotFoundException">
        /// The <paramref name="remotePath"/> file does not exist.
        /// </exception>
        public void UploadFile( string remotePath, string localFilePath ) {
            if( !File.Exists( localFilePath ) ) {
                throw new FileNotFoundException( "The file does not exist", localFilePath );
            }

            this._request = GetNewRequest( Host + remotePath );
            this._request.Method = WebRequestMethods.Ftp.UploadFile;
            FileStream localStream = new FileStream( localFilePath, FileMode.Open );
            byte[] byteBuffer = new byte[ this._bufferSize ];

            try {
                this._stream = this._request.GetRequestStream();
                int bytesSent = localStream.Read( byteBuffer, 0, this._bufferSize );
                while( bytesSent != 0 ) {
                    this._stream.Write( byteBuffer, 0, bytesSent );
                    bytesSent = localStream.Read( byteBuffer, 0, this._bufferSize );
                }
            }
            catch( Exception e ) {
                throw;
            }
            finally {
                this._request = null;
                if( this._stream != null ) { this._stream.Close(); }
                if( localStream != null ) { localStream.Close(); }
            }
        }

        /// <summary>
        /// Uploads a local file to the server.
        /// </summary>
        /// <param name="file">The <see cref="FtpFile"/> to upload to.</param>
        /// <param name="localFilePath">The path to the local file.</param>
        /// <exception cref="ArgumentNullException">
        /// The <paramref name="file"/> argument is null.
        /// </exception>
        /// <exception cref="UriFormatException">
        /// The <paramref name="file"/> name property is empty.
        /// </exception>
        public void UploadFile( FtpFile file, string localFilePath ) {
            if( file == null ) {
                throw new ArgumentNullException( "file", "The FtpFile can not be null" );
            }

            if( string.IsNullOrWhiteSpace( file.Name ) ) {
                throw new UriFormatException( "The FtpFile's Name property can not be empty" );
            }

            UploadFile( file.Path + file.Name, localFilePath );
        }

        /// <summary>
        /// Deletes a file from the server.
        /// </summary>
        /// <param name="remoteFilePath">The path to the remote file [minus the the host].</param>
        /// <exception cref="FileNotFoundException">
        /// The <paramref name="remoteFilePath"/> does not exist.
        /// </exception>
        public void DeleteFile( string remoteFilePath ) {
            if( !FtpItemExists( remoteFilePath, FtpItemType.File ) ) {
                throw new FileNotFoundException( "The file does not exist", Host + remoteFilePath );
            }

            this._request = GetNewRequest( Host + remoteFilePath );
            this._request.Method = WebRequestMethods.Ftp.DeleteFile;

            try {
                this._response = ( FtpWebResponse )this._request.GetResponse();
            }
            catch( Exception e ) {
                throw;
            }
            finally {
                if( this._response != null ) { this._response.Close(); }
                this._request = null;
            }
        }

        /// <summary>
        /// Deletes a file from the server.
        /// </summary>
        /// <param name="file">The <see cref="FtpFile"/> to delete.</param>
        /// <exception cref="ArgumentNullException">
        /// The <paramref name="file"/> argument is null.
        /// </exception>
        public void DeleteFile( FtpFile file ) {
            if( file == null ) {
                throw new ArgumentNullException( "file", "The FtpFile can not be null" );
            }

            if( string.IsNullOrWhiteSpace( file.Name ) ) {
                throw new UriFormatException( "The FtpFile's Name property can not be empty" );
            }

            DeleteFile( file.Path + file.Name );
        }

        /// <summary>
        /// Renames a file on the server.
        /// </summary>
        /// <param name="remoteFilePath">The path to the remote file [minus the the host].</param>
        /// <param name="newName">The new file name to use.</param>
        /// <exception cref="FileNotFoundException">
        /// The <paramref name="remoteFilePath"/> does not exist.
        /// </exception>
        public void RenameFile( string remoteFilePath, string newName ) {
            if( !FtpItemExists( remoteFilePath, FtpItemType.File ) ) {
                throw new FileNotFoundException( "The file does not exist", Host + remoteFilePath );
            }

            this._request = GetNewRequest( Host + remoteFilePath );
            this._request.Method = WebRequestMethods.Ftp.Rename;

            try {
                this._request.RenameTo = newName;
                this._response = ( FtpWebResponse )this._request.GetResponse();
            }
            catch( Exception e ) {
                throw;
            }
            finally {
                if( this._response != null ) { this._response.Close(); }
                this._request = null;
            }
        }

        /// <summary>
        /// Renames a file on the server.
        /// </summary>
        /// <param name="file">The <see cref="FtpFile"/> to rename.</param>
        /// <param name="newName">The new file name to use.</param>
        /// <exception cref="ArgumentNullException">
        /// The <paramref name="file"/> argument is null.
        /// </exception>
        public void RenameFile( FtpFile file, string newName ) {
            if( file == null ) {
                throw new ArgumentNullException( "file", "The FtpFile can not be null" );
            }

            if( string.IsNullOrWhiteSpace( file.Name ) ) {
                throw new UriFormatException( "The FtpFile's Name property can not be empty" );
            }

            RenameFile( file.Path + file.Name, newName );
        }

        /// <summary>
        /// Gets the file size of the specified file.
        /// </summary>
        /// <param name="remoteFilePath">The path to the remote file [minus the the host].</param>
        /// <returns>A long value representing the size of the file.</returns>
        /// <exception cref="FileNotFoundException">
        /// The <paramref name="remoteFilePath"/> does not exist.
        /// </exception>
        public long FileSize( string remoteFilePath ) {
            if( !FtpItemExists( remoteFilePath, FtpItemType.File ) ) {
                throw new FileNotFoundException( "The file does not exist", Host + remoteFilePath );
            }

            this._request = GetNewRequest( Host + remoteFilePath );
            this._request.Method = WebRequestMethods.Ftp.GetFileSize;

            try {
                this._response = ( FtpWebResponse )this._request.GetResponse();
                this._stream = this._response.GetResponseStream();
                string size = string.Empty;

                using( StreamReader reader = new StreamReader( this._stream ) ) {
                    while( reader.Peek() != -1 ) {
                        size = reader.ReadToEnd();
                    }
                }

                long s;
                if( long.TryParse( size, out s ) ) {
                    return s;
                }
                else {
                    return 0L;
                }
            }
            catch( Exception e ) {
                return 0L;
            }
            finally {
                if( this._response != null ) { this._response.Close(); }
                if( this._stream != null ) { this._stream.Close(); }
                this._request = null;
            }
        }

        /// <summary>
        /// Gets the file size of the specified file.
        /// </summary>
        /// <param name="item">The <see cref="FtpFile"/> to get the size of.</param>
        /// <returns>A long value representing the size of the file.</returns>
        /// <exception cref="FileNotFoundException">
        /// The <paramref name="item"/> argument is null.
        /// </exception>
        /// <exception cref="UriFormatException">
        /// The <paramref name="item"/> name property is empty.
        /// </exception>
        public long FileSize( FtpItem item ) {
            if( item == null ) {
                throw new ArgumentNullException( "item", "The FtpItem can not be null" );
            }

            if( string.IsNullOrWhiteSpace( item.Name ) ) {
                throw new UriFormatException( "The FtpItem's Name property can not be empty" );
            }

            item.Size = FileSize( item.Path + item.Name );
            return item.Size;
        }

        /// <summary>
        /// Gets the server files last write time.
        /// </summary>
        /// <param name="remoteFilePath">The path to the remote file [minus the the host].</param>
        /// <returns>A nullable <see cref="System.DateTime"/> representing the file's last write time.</returns>
        /// <exception cref="FileNotFoundException">
        /// The <paramref name="remoteFilePath"/> does not exist.
        /// </exception>
        public DateTime? LastWriteTime( string remoteFilePath ) {
            if( !FtpItemExists( remoteFilePath, FtpItemType.File ) ) {
                throw new FileNotFoundException( "The item does not exist", Host + remoteFilePath );
            }

            this._request = GetNewRequest( Host + remoteFilePath );
            this._request.Method = WebRequestMethods.Ftp.GetDateTimestamp;

            try {
                this._response = ( FtpWebResponse )this._request.GetResponse();
                this._stream = this._response.GetResponseStream();
                string date = string.Empty;

                using( StreamReader ftpReader = new StreamReader( this._stream ) ) {
                    date = ftpReader.ReadToEnd();
                }

                return DateTime.Parse( date );
            }
            catch( Exception e ) {
                return null;
            }
            finally {
                if( this._response != null ) { this._response.Close(); }
                if( this._stream != null ) { this._stream.Close(); }
                this._request = null;
            }
        }

        /// <summary>
        /// Gets the server files last write time.
        /// </summary>
        /// <param name="file">The FtpFile to get the last write time of.</param>
        /// <returns>A nullable <see cref="System.DateTime"/> representing the file's last write time.</returns>
        /// <exception cref="ArgumentNullException">
        /// The <paramref name="file"/> argument is null.
        /// </exception>
        /// <exception cref="UriFormatException">
        /// The <paramref name="file"/> name property is empty.
        /// </exception>
        public DateTime? LastWriteTime( FtpFile file ) {
            if( file == null ) {
                throw new ArgumentNullException( "file", "The FtpItem can not be null" );
            }

            if( string.IsNullOrWhiteSpace( file.Name ) ) {
                throw new UriFormatException( "The FtpItem's Name property can not be empty" );
            }

            file.Timestamp = LastWriteTime( file.Path + file.Name );
            return file.Timestamp;
        }

        /// <summary>
        /// Creates a new directory on the server.
        /// </summary>
        /// <param name="newDirectory">The path to the remote directory [minus the the host].</param>
        public void CreateDirectory( string newDirectory ) {
            this._request = GetNewRequest( Host + newDirectory );
            this._request.Method = WebRequestMethods.Ftp.MakeDirectory;

            try {
                this._response = ( FtpWebResponse )this._request.GetResponse();
            }
            catch( Exception e ) {
                throw;
            }
            finally {
                if( this._response != null ) { this._response.Close(); }
                this._request = null;
            }
        }

        /// <summary>
        /// Creates a new directory on the server.
        /// </summary>
        /// <param name="directory">The <see cref="FtpDirectory"/> to create.</param>
        /// <exception cref="ArgumentNullException">
        /// The <paramref name="directory"/> argument is null.
        /// </exception>
        public void CreateDirectory( FtpDirectory directory ) {
            if( directory == null ) {
                throw new ArgumentNullException( "directory", "The FtpDirectory can not be null" );
            }

            CreateDirectory( directory.Path + directory.Name );
        }

        /// <summary>
        /// Deletes a directory from the server.
        /// </summary>
        /// <param name="directory">The path to the remote directory [minus the the host].</param>
        /// <exception cref="FileNotFoundException">
        /// The <paramref name="directory"/> does not exist.
        /// </exception>
        public void DeleteDirectory( string directory ) {
            if( !FtpItemExists( directory, FtpItemType.Folder ) ) {
                throw new FileNotFoundException( "The directory does not exist", Host + directory );
            }

            this._request = GetNewRequest( Host + directory );
            this._request.Method = WebRequestMethods.Ftp.RemoveDirectory;

            try {
                this._response = ( FtpWebResponse )this._request.GetResponse();
            }
            catch( Exception e ) {
                throw;
            }
            finally {
                if( this._response != null ) { this._response.Close(); }
                this._request = null;
            }
        }

        /// <summary>
        /// Deletes a directory from the server.
        /// </summary>
        /// <param name="directory">The <see cref="FtpDirectory"/> to delete.</param>
        /// <exception cref="ArgumentNullException">
        /// The <paramref name="directory"/> argument is null.
        /// </exception>
        /// <exception cref="UriFormatException">
        /// The <paramref name="directory"/> name property is empty.
        /// </exception>
        public void DeleteDirectory( FtpDirectory directory ) {
            if( directory == null ) {
                throw new ArgumentNullException( "directory", "The FtpDirectory can not be null" );
            }

            if( string.IsNullOrWhiteSpace( directory.Name ) ) {
                throw new UriFormatException( "The FtpDirectory's Name property can not be empty" );
            }

            DeleteDirectory( directory.Path + directory.Name );
        }

        /// <summary>
        /// Gets a simple, string list of the sub-directories in the given directory.
        /// </summary>
        /// <param name="directory">The server path to get the directories from [minus the the host].</param>
        /// <returns>A string array of all the sub-directories of the specified sever path.</returns>
        public string[] SimpleDirectoryList( string directory ) {
            // Since using the WebRequestMethods.Ftp.ListDirectory does, in fact, return files too
            // I'm just going to do that same thing as the SimpleFileList
            return DirectoryList( directory ).Select( d => d.Name ).ToArray();
        }

        /// <summary>
        /// Gets a simple, string list of the sub-directories in the given directory.
        /// </summary>
        /// <param name="directory">The <see cref="FtpDirectory"/> to get the sub-directories from.</param>
        /// <returns>A string array of all the sub-directories of the specified <see cref="FtpDirectory"/>.</returns>
        /// <exception cref="ArgumentNullException">
        /// The <paramref name="directory"/> argument is null.
        /// </exception>
        public string[] SimpleDirectoryList( FtpDirectory directory ) {
            if( directory == null ) {
                throw new ArgumentNullException( "directory", "The FtpDirectory can not be null" );
            }

            return SimpleDirectoryList( directory.Path + directory.Name );
        }

        /// <summary>
        /// Gets a simple, string list of the files in the given directory.
        /// </summary>
        /// <param name="directory">The server path to get the files from [minus the the host].</param>
        /// <returns>A string array of all the files of the specified sever path.</returns>
        public string[] SimpleFileList( string directory ) {
            return FileList( directory ).Select( f => f.Name ).ToArray();
        }

        /// <summary>
        /// Gets a simple, string list of the files in the given directory.
        /// </summary>
        /// <param name="directory">The <see cref="FtpDirectory"/> to get the files from.</param>
        /// <returns>A string array of all the files in the specified <see cref="FtpDirectory"/>.</returns>
        public string[] SimpleFileList( FtpDirectory directory ) {
            return FileList( directory ).Select( f => f.Name ).ToArray();
        }

        /// <summary>
        /// Gets a list of <see cref="FtpDirectory"/> objects from the given directory.
        /// </summary>
        /// <param name="directory">The server path to get the directories from [minus the the host].</param>
        /// <returns>A list of <see cref="FtpDirectory"/> from the given directory.</returns>
        public List<FtpDirectory> DirectoryList( string directory ) {
            var list = new List<FtpDirectory>();
            string path;
            var output = GetFtpListOutput( directory );

            if( directory == string.Empty ) {
                path = string.Empty;
            }
            else {
                path = directory.Substring( 0, directory.LastIndexOf( '/' ) );
            }

            foreach( string line in output ) {
                var match = LineMatch( line );
                if( match == null ) {
                    throw new ApplicationException( "Unable to parse line: " + line );
                }

                FtpItem item = BuildItem( match, path );

                if( item.GetType() == typeof( FtpDirectory ) ) {
                    list.Add( ( FtpDirectory )item );
                }
            }

            return list;
        }

        /// <summary>
        /// Gets a list of <see cref="FtpDirectory"/> objects from the given directory.
        /// </summary>
        /// <param name="directory">The <see cref="FtpDirectory"/> to get the sub-directories from.</param>
        /// <returns>A list of <see cref="FtpDirectory"/> from the given directory.</returns>
        /// <exception cref="ArgumentNullException">
        /// The <paramref name="directory"/> argument is null.
        /// </exception>
        public List<FtpDirectory> DirectoryList( FtpDirectory directory ) {
            if( directory == null ) {
                throw new ArgumentNullException( "directory", "The FtpDirectory can not be null" );
            }

            return DirectoryList( directory.Path + directory.Name );
        }

        /// <summary>
        /// Gets a list of <see cref="FtpFile"/> objects from the given directory.
        /// </summary>
        /// <param name="directory">The server path to get the files from [minus the the host].</param>
        /// <returns>A list of <see cref="FtpFile"/> from the given directory.</returns>
        public List<FtpFile> FileList( string directory ) {
            var list = new List<FtpFile>();
            string path;
            var output = GetFtpListOutput( directory );

            if( directory == string.Empty ) {
                path = string.Empty;
            }
            else {
                path = directory.Substring( 0, directory.LastIndexOf( '/' ) );
            }

            foreach( string line in output ) {
                var match = LineMatch( line );
                if( match == null ) {
                    throw new ApplicationException( "Unable to parse line: " + line );
                }

                FtpItem item = BuildItem( match, path );

                if( item.GetType() == typeof( FtpFile ) ) {
                    list.Add( ( FtpFile )item );
                }
            }

            return list;
        }

        /// <summary>
        /// Gets a list of <see cref="FtpFile"/> objects from the given directory.
        /// </summary>
        /// <param name="directory">The <see cref="FtpDirectory"/> to get the files from.</param>
        /// <returns>A list of <see cref="FtpFile"/> from the given directory.</returns>
        /// <exception cref="ArgumentNullException">
        /// The <paramref name="directory"/> argument is null.
        /// </exception>
        public List<FtpFile> FileList( FtpDirectory directory ) {
            if( directory == null ) {
                throw new ArgumentNullException( "directory", "The FtpDirectory can not be null" );
            }

            return FileList( directory.Path + directory.Name );
        }

        // CreateFile

        /// <summary>
        /// Releases all resources used.
        /// </summary>
        public void Dispose() {
            Dispose( true );
            GC.SuppressFinalize( this );
        }

        /// <summary>
        /// Releases all resources used.
        /// </summary>
        /// <param name="disposing">A boolean value indicating whether or not to dispose managed resources.</param>
        protected virtual void Dispose( bool disposing ) {
            if( !this._disposed ) {
                if( disposing ) {
                    if( this._request != null ) {
                        this._request = null;
                    }

                    if( this._response != null ) {
                        this._response.Close();
                        this._response = null;
                    }

                    if( this._stream != null ) {
                        this._stream.Close();
                        this._stream = null;
                    }
                }

                this._disposed = true;
            }
        }

        // Builds out the FtpWebRequest. Requires the full path => Host + Path + FileName?
        private FtpWebRequest GetNewRequest( string path ) {
            FtpWebRequest tmp = ( FtpWebRequest )FtpWebRequest.Create( "ftp://" + path );

            if( !string.IsNullOrWhiteSpace( Username ) && !string.IsNullOrWhiteSpace( Password ) ) {
                tmp.Credentials = new NetworkCredential( Username, Password );
            }

            tmp.EnableSsl = IsSsl;
            tmp.UseBinary = true;
            tmp.UsePassive = true;
            tmp.KeepAlive = true;

            return tmp;
        }

        private string[] GetFtpListOutput( string directory ) {
            if( !FtpItemExists( directory, FtpItemType.Folder ) ) {
                throw new FileNotFoundException( "The directory does not exist", Host + directory );
            }

            this._request = GetNewRequest( Host + directory );
            this._request.Method = WebRequestMethods.Ftp.ListDirectoryDetails;
            List<string> dirList = new List<string>();

            try {
                this._response = ( FtpWebResponse )this._request.GetResponse();
                this._stream = this._response.GetResponseStream();
                using( StreamReader ftpReader = new StreamReader( this._stream ) ) {
                    while( ftpReader.Peek() != -1 ) {
                        dirList.Add( ftpReader.ReadLine() );
                    }
                }

                return dirList.ToArray();
            }
            catch( Exception e ) {
                return new string[] { string.Empty };
            }
            finally {
                if( this._response != null ) { this._response.Close(); }
                if( this._stream != null ) { this._stream.Close(); }
                this._request = null;
            }
        }

        private Match LineMatch( string line ) {
            Regex reg;
            Match match;
            foreach( string r in this._regEx ) {
                reg = new Regex( r );
                match = reg.Match( line );
                if( match.Success ) {
                    return match;
                }
            }

            return null;
        }

        private DateTime ParseTimestamp( Match match ) {
            if( match.Groups[ "timestamp" ].Value == string.Empty ) {
                throw new ArgumentException( "Match timestamp empty", "match" );
            }

            int year = match.Groups[ "year" ].Value == string.Empty ?
                       DateTime.Now.Year :
                       int.Parse( match.Groups[ "year" ].Value );
            int day = match.Groups[ "day" ].Value == string.Empty ?
                      1 :
                      int.Parse( match.Groups[ "day" ].Value );
            int month = GetMonth( match.Groups[ "month" ].Value );
            int hour = match.Groups[ "hour" ].Value == string.Empty ?
                       12 :
                       int.Parse( match.Groups[ "hour" ].Value );
            int min = match.Groups[ "minute" ].Value == string.Empty ?
                      12 :
                      int.Parse( match.Groups[ "minute" ].Value );

            return new DateTime( year, month, day, hour, min, 0 );
        }

        private int GetMonth( string month ) {
            int m;
            if( int.TryParse( month, out m ) ) {
                return m;
            }

            return DateTime.ParseExact( month, "MMM", CultureInfo.InvariantCulture ).Month;
        }

        private FtpItem BuildItem( Match match, string path ) {
            int code;
            long size;
            FtpItem item = new FtpDirectory();

            if( match.Groups[ "dir" ].Value == "-" ||
                match.Groups[ "dir" ].Value == string.Empty ) {
                item = new FtpFile();
            }
            else {
                item = new FtpDirectory();
            }

            item.FileCode = int.TryParse( match.Groups[ "filecode" ].Value, out code ) ? code : -1;
            item.Group = match.Groups[ "group" ].Value;
            item.Host = Host;
            item.Name = match.Groups[ "name" ].Value;
            item.Owner = match.Groups[ "owner" ].Value;
            item.Path = path;
            item.Permissions = match.Groups[ "permission" ].Value;
            item.Size = long.TryParse( match.Groups[ "size" ].Value, out size ) ? size : 0;
            item.Timestamp = ParseTimestamp( match );

            return item;
        }

        private bool OnValidateCertificate( object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors ) {
            return true;
        }
    }
}

// http://ftpclient.codeplex.com/SourceControl/latest#5336
// http://stackoverflow.com/questions/7060983/c-sharp-class-to-parse-webrequestmethods-ftp-listdirectorydetails-ftp-response
// http://www.codeproject.com/Tips/443588/Simple-Csharp-FTP-Class
// http://www.codeproject.com/Articles/293391/File-Transfer-Protocol-FTP-Client
// http://www.dreamincode.net/forums/topic/35902-create-an-ftp-class-library-in-c%23/
// http://ftplib.codeplex.com/SourceControl/changeset/view/82946#589935

/*
    drwxr-x---   16 bryancki   99               4096 Dec 30 07:57 .
    drwx--x--x   14 bryancki   bryancki         4096 Dec 30 07:56 ..
    -rw-r--r--    1 bryancki   bryancki         9469 Oct 24  2012 .htaccess
    -rw-r--r--    1 bryancki   bryancki         2836 Jan 20 03:44 404.php
    drwxr-xr-x    2 bryancki   bryancki         4096 Oct 28  2012 about
    drwxr-xr-x    2 bryancki   bryancki         4096 Jul 12  2012 cgi-bin
    drwxr-xr-x    5 bryancki   99               4096 Nov  7  2012 colorconvert
    drwxr-xr-x    3 bryancki   bryancki         4096 Sep 18  2012 css
 
 
    06-25-09  02:41PM            144700153 image34.gif
*/

// Primary *nix Return
// (?<dir>[\-ld])(?<permission>([\-r][\-w][\-xs]){3})\s+(?<filecode>\d+)\s+(?<owner>\w+)\s+(?<group>\w+)\s+(?<size>\d+)\s+(?<timestamp>((?<month>\w{3})\s+(?<day>\d{1,2})\s+(?<hour>\d{1,2}):(?<minute>\d{2}))|((?<month>\w{3})\s+(?<day>\d{1,2})\s+(?<year>\d{4})))\s+(?<name>.+)

// Win 'DOS' Return
// (?<timestamp>(?<month>\d{2})\-(?<day>\d{2})\-(?<year>\d{2})\s+(?<hour>\d{2}):(?<minute>\d{2})(?<noon>[Aa|Pp][mM]))\s+(?<dir>\<\w+\>){0,1}(?<size>\d+){0,1}\s+(?<name>.+)


// 



// "(?<dir>[\\-d])(?<permission>([\\-r][\\-w][\\-xs]){3})\\s+\\d+\\s+\\w+\\s+\\w+\\s+(?<size>\\d+)\\s+(?<timestamp>\\w+\\s+\\d+\\s+\\d{4})\\s+(?<name>.+)", 
// "(?<dir>[\\-d])(?<permission>([\\-r][\\-w][\\-xs]){3})\\s+\\d+\\s+\\d+\\s+(?<size>\\d+)\\s+(?<timestamp>\\w+\\s+\\d+\\s+\\d{4})\\s+(?<name>.+)", 
// "(?<dir>[\\-d])(?<permission>([\\-r][\\-w][\\-xs]){3})\\s+\\d+\\s+\\d+\\s+(?<size>\\d+)\\s+(?<timestamp>\\w+\\s+\\d+\\s+\\d{1,2}:\\d{2})\\s+(?<name>.+)", 
// "(?<dir>[\\-d])(?<permission>([\\-r][\\-w][\\-xs]){3})\\s+\\d+\\s+\\w+\\s+\\w+\\s+(?<size>\\d+)\\s+(?<timestamp>\\w+\\s+\\d+\\s+\\d{1,2}:\\d{2})\\s+(?<name>.+)", 
// "(?<dir>[\\-d])(?<permission>([\\-r][\\-w][\\-xs]){3})(\\s+)(?<size>(\\d+))(\\s+)(?<ctbit>(\\w+\\s\\w+))(\\s+)(?<size2>(\\d+))\\s+(?<timestamp>\\w+\\s+\\d+\\s+\\d{2}:\\d{2})\\s+(?<name>.+)",


/* Available matches
            dir => if it is, will equal 'd' or != null/empty
            permission (not MSDOS)
            timestamp
            month
            day
            year
            hour
            minute
            noon (MSDOS)
            name
            filecode (not MSDOS)
            owner (not MSDOS)
            group (not MSDOS)
            size (not MSDOS)
         */