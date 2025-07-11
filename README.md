# Five9 Reports API

Python/C# solutions for automating the retrieval of eports from the Five9 API with optional SFTP uploading.

- Run Five9 Call Log reports with configurable date ranges
- Save reports as CSV files
- Upload reports to SFTP servers (optional)
- Timestamped file organization

## Installation

```bash
git clone https://github.com/tylerbroussard/five9_reports_api.git
cd five9_reports_api
pip install requests paramiko
```

## Usage

Two versions are available:

### Environment Variable Version
```bash
python five9_reports_api_envvar.py
```

Set required environment variables:
```bash
export FIVE9_USERNAME="your-username"
export FIVE9_PASSWORD="your-password"

# Optional SFTP configuration
export SFTP_HOST="sftp.example.com"
export SFTP_PORT="22"
export SFTP_USERNAME="ftpuser"
export SFTP_PASSWORD="ftppass"
export SFTP_PATH="/reports/"

python five9_reports_api_envvar.py
```

### Command Line Version
```bash
python five9_api_reports_cl.py "username@domain.com:password" [OPTIONS]
```

#### Command Line Options
- `--sftp-host` - SFTP server hostname
- `--sftp-port` - SFTP server port  
- `--sftp-username` - SFTP username
- `--sftp-password` - SFTP password
- `--sftp-path` - SFTP upload path

#### Examples

Basic usage:
```bash
python five9_api_reports_cl.py "user@company.com:mypassword"
```

With SFTP upload:
```bash
python five9_api_reports_cl.py "user@company.com:mypassword" \
  --sftp-host sftp.example.com \
  --sftp-username ftpuser \
  --sftp-password ftppass \
  --sftp-path /reports/
```

## Default Config

- **Report Type**: Call Log report from "Shared Reports" folder
- **Date Range**: Previous 7 days
- **Timeout**: 300 seconds
- **Output**: Timestamped CSV files

## Requirements

- Python 3.6+
- Five9 account with API access
