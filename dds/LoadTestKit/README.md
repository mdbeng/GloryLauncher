# Glory Launcher Load Test Kit

This is a standalone load testing tool for testing your server's anti-DDoS protection.

## Files Included:
- `RunLoadTest.bat` - Main script to run the load test
- `LoadTester.cs` - C# source code for the load tester
- `README.md` - This file

## Requirements:
- .NET SDK installed on your system
- Internet connection
- Target server: https://gloryot.com/login.php

## Configuration:
- **500,000 total requests**
- **2,000 concurrent connections**
- **2-second timeout per request**
- **POST requests to login endpoint**

## How to Use:
1. Double-click `RunLoadTest.bat`
2. Press ENTER to start the aggressive load test
3. Press ESC to exit without running

## Warning:
⚠️ **This will severely impact your internet connection during the test!**
- Expect significant lag for other internet activities
- May consume all available bandwidth
- Run during off-hours when you don't need internet for other tasks

## Expected Results:
- Should generate 1000+ requests per second
- Will likely trigger anti-DDoS protection systems
- May cause temporary connection issues
- Monitor your VPS provider dashboard for DDoS alerts

## Troubleshooting:
- Ensure .NET SDK is installed
- Check that both files are in the same folder
- Verify internet connection is stable
- Consider reducing concurrent connections if system struggles

---
**Note:** This tool is designed for testing your own servers only. Do not use against servers you don't own.