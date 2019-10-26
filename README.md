# h8dutility-source
Enhancements to Les Bird's utility for reading H8 files
Added enhancements to the H8Utility program to better support CP/M Files
•	Version 1.52
  o	Added CP/M file extract capability
  o	Added initialization for Folder button to read directory from last saved working directory
  o	Added try/catch to ReadCPMDir Entry for empty images
  o	Added capability to select Boot ROM from directory listing
  o	Changed CPM File size message to indicate bytes instead of KB
  o	Added disk size calculation based on stored size to account for 400k .H8D disks
  o	Changed from ASCIIencoding to UTF8Encoding to deal with ASCII chars with bit 7 set high being encoded as ‘?’ instead of the proper character
•	Version 1.60
  o	Added CP/M capability to Add button for H8D disks
•	Version 1.70
  o	Added .imd file read capability 
•	Version 1.8 (in development)
  o	Added .IMD CP/M file extraction
  o	Added .IMD File addition
