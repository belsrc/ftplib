[![No Maintenance Intended](https://img.shields.io/badge/No%20Maintenance%20Intended-x-red.svg?style=flat-square&longCache=true)](http://unmaintained.tech/)

# ftplib

A quick little library that I put together for other projects. It's really just a wrapper for FtpWebRequest with a friendlier interface.


#### Quick Example
```cs
string[] dirs;
List<FtpFile> files;

using( var con = new FtpConnection( "ftp.somehost.com", 22, "username", "password", true ) ) {
    string path = "/some/folder/location/";
    if( con.FtpItemExists( path, FtpItemType.Folder ) ) {
        dirs = con.SimpleDirectoryList( path );
        files = con.FileList( path );
    }
    
    foreach( var file in files ) {
        // Download each file and save it to the current
        // path with the same name.
        con.DownloadFile( file, file.Name )
    }
}
```

### License
ftplib is released under a BSD 3-Clause License

Copyright &copy; 2013, Bryan Kizer
All rights reserved. 

Redistribution and use in source and binary forms, with or without 
modification, are permitted provided that the following conditions are 
met: 

* Redistributions of source code must retain the above copyright notice, 
  this list of conditions and the following disclaimer.
* Redistributions in binary form must reproduce the above copyright notice,
  this list of conditions and the following disclaimer in the documentation
  and/or other materials provided with the distribution.
* Neither the name of the Organization nor the names of its contributors 
  may be used to endorse or promote products derived from this software 
  without specific prior written permission. 
  
THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS 
IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED 
TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A 
PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT 
HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, 
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED 
TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR 
PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF 
LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING 
NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS 
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE. 
