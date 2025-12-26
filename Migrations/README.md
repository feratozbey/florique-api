# Database Migrations

This folder contains database migration scripts for the Florique API.

## How to Apply Migrations

### SQL Server
Execute the migration scripts with the `_SqlServer.sql` suffix:
```sql
sqlcmd -S your_server -d your_database -i 001_AddUserCreationFields_SqlServer.sql
```

Or using SQL Server Management Studio (SSMS):
1. Open SSMS and connect to your database
2. Open the migration file
3. Execute the script

### PostgreSQL
Execute the migration scripts with the `_PostgreSQL.sql` suffix:
```bash
psql -h your_host -d your_database -U your_user -f 001_AddUserCreationFields_PostgreSQL.sql
```

Or using pgAdmin:
1. Open pgAdmin and connect to your database
2. Open the Query Tool
3. Load and execute the migration file

## Migration History

- **001_AddUserCreationFields** - Adds `createdDate` and `deviceType` columns to the users table
  - `createdDate`: Timestamp when the user was created
  - `deviceType`: Type of device used during registration (e.g., "iOS", "Android", "Web")

- **002_AddIpAndLocation** - Adds `ipAddress` and `location` columns to the users table
  - `ipAddress`: IP address from which the user registered (max 45 chars for IPv6)
  - `location`: Geographic location of the user (e.g., "New York, US", "London, UK")
