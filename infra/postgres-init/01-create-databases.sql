-- Order and Payment each own a separate PostgreSQL database (ADR-006). The shared postgres
-- container starts with only the default `delivery` DB, and EF Core's EnsureCreatedAsync builds
-- the SCHEMA but NOT the database itself -- so the databases must already exist.
--
-- The official postgres image executes every *.sql in /docker-entrypoint-initdb.d on FIRST
-- initialisation (empty data volume). CREATE DATABASE cannot run inside a transaction and has no
-- IF NOT EXISTS, so we guard each with a conditional guc/SELECT pattern.
SELECT 'CREATE DATABASE "order"'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'order')\gexec

SELECT 'CREATE DATABASE "payment"'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'payment')\gexec
