# Quick script to check configuration in PostgreSQL database
# Checks what OPENAI_MODEL value is stored

$env:PGPASSWORD = "npg_UBgisLeoK9X2"

Write-Host "=== Checking OPENAI_MODEL in Database ===" -ForegroundColor Cyan

$query = "SELECT configkey, configvalue, isencrypted, updateddate FROM configurations WHERE configkey = 'OPENAI_MODEL';"

& psql -h "ep-jolly-bush-a79trleo-pooler.ap-southeast-2.aws.neon.tech" -p 5432 -U neondb_owner -d neondb -c $query

Write-Host "`n=== All Configuration Keys ===" -ForegroundColor Cyan
$allConfigsQuery = "SELECT configkey, configvalue, isencrypted, updateddate FROM configurations ORDER BY configkey;"
& psql -h "ep-jolly-bush-a79trleo-pooler.ap-southeast-2.aws.neon.tech" -p 5432 -U neondb_owner -d neondb -c $allConfigsQuery

Remove-Item Env:\PGPASSWORD
