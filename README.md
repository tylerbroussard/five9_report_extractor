# Five9 Reports API

A multi-language solution for automating the retrieval of Call Log reports from the Five9 API with optional SFTP uploading capabilities. This repository includes implementations in both Python and C#.

## Features

- Run Five9 Call Log reports with configurable date ranges
- Process and save reports as CSV files
- Upload reports to SFTP servers (optional)
- Detailed progress reporting and summary output
- Clean error handling and timeout management

## Python Implementation

### Requirements

- Python 3.6+
- Required packages:
  - `requests`
  - `paramiko`
  - `argparse`

### Installation

```bash
# Clone the repository
git clone https://github.com/yourusername/five9-report-runner.git

# Install dependencies
pip install requests paramiko
```

### Usage

```bash
python five9_report_runner.py "username@domain.com:password" [--sftp-host HOST] [--sftp-port PORT] [--sftp-username USERNAME] [--sftp-password PASSWORD] [--sftp-path PATH]
```

## C# Implementation

### Requirements

- .NET 6.0 or higher
- NuGet packages:
  - System.CommandLine
  - SSH.NET

### Installation

```bash
# Clone the repository
git clone https://github.com/yourusername/five9-report-runner.git

# Restore NuGet packages
dotnet restore

# Build the solution
dotnet build
```

### Usage

```bash
Five9ReportRunner.exe "username@domain.com:password" [--sftp-host HOST] [--sftp-port PORT] [--sftp-username USERNAME] [--sftp-password PASSWORD] [--sftp-path PATH]
```

## Common Features

Both implementations offer the same core functionality:

| Feature | Python | C# |
|---------|--------|-----|
| Five9 API Authentication | Basic Auth via requests | Basic Auth via HttpClient |
| Report Execution | SOAP API | SOAP API |
| XML Parsing | xml.etree.ElementTree | System.Xml.Linq (XDocument) |
| SFTP Support | paramiko | SSH.NET |
| Command Line Interface | argparse | System.CommandLine |
| Async Operations | No (synchronous) | Yes (async/await) |

## Output

- Creates a timestamped directory for each run
- Saves reports as CSV files with sanitized filenames
- Provides detailed console output with progress and error reporting
- Displays summary statistics upon completion

## Default Behavior

- The application retrieves the Call Log report from the "Shared Reports" folder
- Default date range is the previous 7 days
- Timeout for report execution is set to 300 seconds (5 minutes)
- Report files include timestamps to prevent overwriting

## Implementation Notes

### Python Version

- Uses synchronous HTTP requests
- Simple command line interface with argparse
- Lightweight implementation with minimal dependencies

### C# Version

- Uses asynchronous programming with async/await
- More structured object-oriented approach
- Stronger typing and error handling
- Modern command line parsing with System.CommandLine
