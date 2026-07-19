# ExtCompress User Manual

Welcome to the ExtCompress CLI Advanced User Manual. ExtCompress is a high-performance compression utility utilizing the Zstandard (zstd) algorithm.

## Commands

### 1. Compress
Compresses one or more files into an `.extc` archive.

**Syntax:**
`ExtCompress.exe /compress "file1" "file2" ... /out "output.extc" [/level 1-22]`

**Parameters:**
- `"file1" "file2" ...`: The files to compress.
- `/out "output.extc"`: The target output archive path.
- `/level 1-22` *(Optional)*: Specifies the Zstandard compression level. 
  - **1-9**: Fast compression.
  - **10-19**: High compression.
  - **20-22**: Ultra-high compression.

### 2. Lightning Mode
A specialized quick-compression mode optimized for speed, equivalent to compression level 1.

**Syntax:**
`ExtCompress.exe /lightning "file1" "file2" ... /out "output.extc"`

### 3. Decompress
Extracts the contents of an `.extc` archive to a specified directory.

**Syntax:**
`ExtCompress.exe /decompress "input.extc" /out "folder"`

## About Zstandard Compression
ExtCompress is powered by Zstandard, offering levels (1-22) to fine-tune the balance between compression speed and ratio.
