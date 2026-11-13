---
name: sql-architect
description: 'Design and optimize PostgreSQL schemas, B-Tree and JSONB GIN indexes, and Entity Framework Core queries. Use when asked about database design or query optimization.'
---

# PostgreSQL & EF Core Architect Skill

Follow these database engineering guidelines:

1. **Indexing Strategy**: Use B-Tree indexes for equality/range filters and GIN indexes for `jsonb` columns.
2. **Entity Framework Core**: Prefer `AsNoTracking()` for read-only queries and avoid N+1 query patterns.
3. **Schema Integrity**: Enforce primary keys, foreign key constraints, and explicit column types.
