import sys
import datetime
from datetime import timedelta
import subprocess
import os
import time
import requests
import base64
import argparse
import paramiko
from pathlib import Path

def get_credentials_from_env():
    """Get Five9 credentials from environment variables"""
    username = os.getenv('FIVE9_USERNAME')
    password = os.getenv('FIVE9_PASSWORD')
    
    if not username or not password:
        raise ValueError(
            "Five9 credentials not found in environment variables. "
            "Please set FIVE9_USERNAME and FIVE9_PASSWORD environment variables."
        )
    
    return f"{username}:{password}"

def get_sftp_config_from_env():
    """Get SFTP configuration from environment variables"""
    host = os.getenv('SFTP_HOST')
    port = int(os.getenv('SFTP_PORT', '22'))
    username = os.getenv('SFTP_USERNAME')
    password = os.getenv('SFTP_PASSWORD')
    path = os.getenv('SFTP_PATH', '/')
    
    if host and username and password:
        return {
            'host': host,
            'port': port,
            'username': username,
            'password': password,
            'path': path
        }
    
    return None

def get_date_ranges():
    today = datetime.datetime.now()
    
    seven_days_ago = today - timedelta(days=7)
    
    start_of_week = today - timedelta(days=today.weekday())
    
    # Format dates in Five9's expected format (Eastern Time)
    date_formats = {
        'today_start': today.replace(hour=0, minute=0, second=0, microsecond=0).strftime("%Y-%m-%dT%H:%M:%S.000-05:00"),
        'today_end': today.replace(hour=23, minute=59, second=59, microsecond=999999).strftime("%Y-%m-%dT%H:%M:%S.000-05:00"),
        'this_week_start': start_of_week.replace(hour=0, minute=0, second=0, microsecond=0).strftime("%Y-%m-%dT%H:%M:%S.000-05:00"),
        'this_week_end': today.replace(hour=23, minute=59, second=59, microsecond=999999).strftime("%Y-%m-%dT%H:%M:%S.000-05:00"),
        'last_week_start': seven_days_ago.replace(hour=0, minute=0, second=0, microsecond=0).strftime("%Y-%m-%dT%H:%M:%S.000-05:00"),
        'last_week_end': today.replace(hour=23, minute=59, second=59, microsecond=999999).strftime("%Y-%m-%dT%H:%M:%S.000-05:00")
    }
    
    return date_formats

def create_output_directory():
    timestamp = datetime.datetime.now().strftime('%Y%m%d_%H%M%S')
    output_dir = f"five9_reports_{timestamp}"
    os.makedirs(output_dir, exist_ok=True)
    return output_dir

def get_clean_filename(report_name):
    # Start with the report name
    clean_name = report_name.lower()  # Convert to lowercase
    
    # Replace special characters
    replacements = {
        '#': 'num',
        '/': '-',
        ' ': '_',
        '–': '-', 
        '&': 'and',
        '%': 'pct',
        '(': '',
        ')': '',
    }
    
    for old, new in replacements.items():
        clean_name = clean_name.replace(old, new)
    
    # Remove any other special characters
    clean_name = ''.join(c for c in clean_name if c.isalnum() or c in ('_', '-'))
    
    # Add timestamp
    timestamp = datetime.datetime.now().strftime('%Y%m%d_%H%M%S')
    return f"{clean_name}_{timestamp}.csv"

def sanitize_report_name(name):
    """
    Sanitize report name by replacing special characters with standard ones
    """
    replacements = {
        '–': '-',  # Replace en-dash with hyphen
        '—': '-',  # Replace em-dash with hyphen
        '"': '"',  # Replace curly quotes
        ''': "'",
        ''': "'"
    }
    
    for old, new in replacements.items():
        name = name.replace(old, new)
    return name

def run_report(credentials, folder, report_name, start_time, end_time):
    # Sanitize the report name before using it in the API call
    report_name = sanitize_report_name(report_name)
    
    soap_request = f'''
    <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ser="http://service.admin.ws.five9.com/">
       <soapenv:Header/>
       <soapenv:Body>
          <ser:runReport>
             <folderName>{folder}</folderName>
             <reportName>{report_name}</reportName>
             <criteria>
                <time>
                   <start>{start_time}</start>
                   <end>{end_time}</end>
                </time>
             </criteria>
          </ser:runReport>
       </soapenv:Body>
    </soapenv:Envelope>
    '''
    
    response = requests.post(
        "https://api.five9.com/wsadmin/v13/AdminWebService",
        headers={
            'Content-Type': 'text/xml',
            'Authorization': f'Basic {base64.b64encode(credentials.encode()).decode()}'
        },
        data=soap_request
    )
    
    if response.status_code != 200:
        raise Exception(f"Failed to run report: {response.text}")
        
    # Extract identifier from response
    import xml.etree.ElementTree as ET
    root = ET.fromstring(response.text)
    return root.find('.//return').text

def check_report_status(credentials, identifier):
    soap_request = f'''
    <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ser="http://service.admin.ws.five9.com/">
       <soapenv:Header/>
       <soapenv:Body>
          <ser:isReportRunning>
             <identifier>{identifier}</identifier>
          </ser:isReportRunning>
       </soapenv:Body>
    </soapenv:Envelope>
    '''
    
    response = requests.post(
        "https://api.five9.com/wsadmin/v13/AdminWebService",
        headers={
            'Content-Type': 'text/xml',
            'Authorization': f'Basic {base64.b64encode(credentials.encode()).decode()}'
        },
        data=soap_request
    )
    
    if response.status_code != 200:
        raise Exception(f"Failed to check report status: {response.text}")
        
    # Parse response to determine if report is still running
    import xml.etree.ElementTree as ET
    root = ET.fromstring(response.text)
    return root.find('.//return').text.lower() == 'true'

def get_report_results(credentials, identifier):
    soap_request = f'''
    <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ser="http://service.admin.ws.five9.com/">
       <soapenv:Header/>
       <soapenv:Body>
          <ser:getReportResultCsv>
             <identifier>{identifier}</identifier>
          </ser:getReportResultCsv>
       </soapenv:Body>
    </soapenv:Envelope>
    '''
    
    response = requests.post(
        "https://api.five9.com/wsadmin/v13/AdminWebService",
        headers={
            'Content-Type': 'text/xml',
            'Authorization': f'Basic {base64.b64encode(credentials.encode()).decode()}'
        },
        data=soap_request
    )
    
    if response.status_code != 200:
        raise Exception(f"Failed to get report results: {response.text}")
        
    # Extract CSV data from response
    import xml.etree.ElementTree as ET
    root = ET.fromstring(response.text)
    return root.find('.//return').text

def run_single_report(report, output_dir, credentials):
    try:
        print(f"  Name: {report['name']}")
        print(f"  Folder: {report['folder']}")
        print(f"  Time Range: {report['start']} to {report['end']}")
        print(f"  Started at: {datetime.datetime.now().strftime('%H:%M:%S')}")
        print("  Executing report...")
        
        
        print("  Calling run_report API...")
        identifier = run_report(
            credentials,
            report['folder'],
            report['name'], 
            report['start'],
            report['end']
        )
        print(f"  Got report identifier: {identifier}")
        
        
        print("  Checking report status...")
        start_time = time.time()
        timeout = 300  
        while check_report_status(credentials, identifier):
            elapsed = time.time() - start_time
            if elapsed > timeout:
                raise Exception(f"Report timed out after {timeout} seconds")
            print(f"  Still running... ({elapsed:.0f} seconds elapsed)")
            time.sleep(5)
            
        print("  Report completed, fetching results...")
        # Get results
        results = get_report_results(credentials, identifier)
        
        # Save to file
        clean_name = get_clean_filename(report['name'])
        filepath = os.path.join(output_dir, clean_name)
        with open(filepath, 'w') as f:
            f.write(results)
            
        print(f"  ✓ Success - Saved to {filepath}")
        return True, filepath  
            
    except Exception as e:
        print(f"  ✗ Error - {str(e)}")
        return False, str(e)  # Return tuple of failure status and error message

def upload_to_sftp(local_file, sftp_config):
    """Upload a file to an SFTP server"""
    try:
        print(f"\nUploading {local_file} to SFTP server {sftp_config['host']}...")
        transport = paramiko.Transport((sftp_config['host'], sftp_config['port']))
        transport.connect(username=sftp_config['username'], password=sftp_config['password'])
        sftp = paramiko.SFTPClient.from_transport(transport)
        
        # Create remote path if it doesn't exist
        remote_path = sftp_config['path']
        try:
            sftp.stat(remote_path)
        except IOError:
            print(f"Remote directory {remote_path} doesn't exist, creating it...")
            sftp.mkdir(remote_path)
        
        # Upload the file
        remote_file = f"{remote_path}/{os.path.basename(local_file)}"
        sftp.put(local_file, remote_file)
        
        print(f"✓ Successfully uploaded to {remote_file}")
        sftp.close()
        transport.close()
        return True
    except Exception as e:
        print(f"✗ SFTP upload failed: {str(e)}")
        return False

def run_reports():
    """Main function to run reports using environment variables for configuration"""
    try:
        # Get credentials from environment variables
        credentials = get_credentials_from_env()
        
        # Get SFTP configuration from environment variables (optional)
        sftp_config = get_sftp_config_from_env()
        
        start_time = datetime.datetime.now()
        dates = get_date_ranges()
        output_dir = create_output_directory()
        
        # Define reports list - modified to only include Call Log report
        reports = [
            {
                "name": "Call Log",
                "folder": "Shared Reports",
                "start": dates['last_week_start'],
                "end": dates['last_week_end']
            }
        ]
        
        # Print the summary information
        print("\n=== Five9 Call Log Report Runner ===")
        print(f"Started at: {datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
        print(f"Output directory: {output_dir}")
        print(f"Total reports to run: {len(reports)}")
        if sftp_config:
            print(f"SFTP upload enabled: {sftp_config['host']}:{sftp_config['port']}")
        else:
            print("SFTP upload disabled (no SFTP configuration found)")
        print("\n=== Date Ranges ===")
        print(f"Date Range: {dates['last_week_start']} to {dates['last_week_end']}")
        print("\n=== Starting Report Execution ===")
        
        successful_reports = 0
        failed_reports = 0
        report_results = []
        
        # Run each report
        for index, report in enumerate(reports, 1):
            print(f"\n[{index}/{len(reports)}] Processing Report:")
            success, result = run_single_report(report, output_dir, credentials)
            
            if success:
                successful_reports += 1
                report_results.append({
                    'name': report['name'],
                    'status': 'Success',
                    'file': result,
                    'duration': (datetime.datetime.now() - start_time).total_seconds()
                })
                
                # Upload to SFTP if configured
                if sftp_config:
                    upload_to_sftp(result, sftp_config)
            else:
                failed_reports += 1
                report_results.append({
                    'name': report['name'],
                    'status': 'Failed',
                    'error': result
                })
            
            print(f"\n  Progress: {index}/{len(reports)} reports processed")
            print(f"  Running totals: {successful_reports} successful, {failed_reports} failed")
        
        # Final summary
        end_time = datetime.datetime.now()
        total_duration = (end_time - start_time).total_seconds()
        
        print("\n=== Final Summary ===")
        print(f"Execution completed time: {end_time.strftime('%Y-%m-%d %H:%M:%S')}")
        print(f"Total duration: {total_duration:.1f} seconds")
        print(f"Total reports: {len(reports)}")
        print(f"Successful: {successful_reports}")
        print(f"Failed: {failed_reports}")
        print(f"Output directory: {output_dir}")
        
        print("\n=== Detailed Report Results ===")
        for result in report_results:
            print(f"\nReport: {result['name']}")
            print(f"Status: {result['status']}")
            if result['status'] == 'Success':
                print(f"Duration: {result['duration']:.1f} seconds")
                print(f"Output: {result['file']}")
            else:
                print(f"Error: {result['error']}")
                
    except ValueError as e:
        print(f"Configuration Error: {e}")
        print("\nRequired environment variables:")
        print("- FIVE9_USERNAME: Your Five9 username")
        print("- FIVE9_PASSWORD: Your Five9 password")
        print("\nOptional SFTP environment variables:")
        print("- SFTP_HOST: SFTP server hostname")
        print("- SFTP_PORT: SFTP server port (default: 22)")
        print("- SFTP_USERNAME: SFTP username")
        print("- SFTP_PASSWORD: SFTP password")
        print("- SFTP_PATH: SFTP remote path (default: /)")
        sys.exit(1)
    except Exception as e:
        print(f"Unexpected error: {e}")
        sys.exit(1)

if __name__ == "__main__":
    parser = argparse.ArgumentParser(
        description='Five9 Call Log Report Runner - Uses environment variables for credentials',
        epilog='''
Environment Variables:
  Required:
    FIVE9_USERNAME    Five9 username
    FIVE9_PASSWORD    Five9 password
  
  Optional (for SFTP upload):
    SFTP_HOST         SFTP server hostname
    SFTP_PORT         SFTP server port (default: 22)
    SFTP_USERNAME     SFTP username
    SFTP_PASSWORD     SFTP password
    SFTP_PATH         SFTP remote path (default: /)
        ''',
        formatter_class=argparse.RawDescriptionHelpFormatter
    )
    
    args = parser.parse_args()
    run_reports()
