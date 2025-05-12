# Primer on Connection Pooling

## Overview
Connection pooling is a technique used to manage database connections efficiently. It involves maintaining a pool of connections that can be reused, reducing the overhead of opening and closing connections frequently.

## Key Concepts
- **Connection Pool Initialization**: A connection pool is created for each unique connection string. The pool maintains a collection of connections that are already logged in and ready to use.
- **Connection Reuse and Creation**: Connections are reused from the pool if available; otherwise, new connections are created. Connection strings are used to differentiate pools, and any change in the connection string results in a new pool.
- **Connection String Sensitivity**: Connection pooling is not sensitive to whitespace in connection strings. Different authentication methods for the same user result in separate pools.
- **Pool Management**: Pools are managed per process. A pool manager oversees all pools and determines which pool to use based on the connection string.
- **Session Settings**: SQL Server provides a procedure (SP reset connection) to reset session settings when reusing a connection. SP reset connection is triggered every time a connection is reused from the pool.
- **Handling Transactions**: Connections involved in transactions are handled separately and may be reset while preserving the transaction state.
- **Connection Liveness**: Connection liveness is checked when pulling connections from the pool. Dead connections are discarded, and new ones are created if necessary.
- **Connection Pruning**: Idle connections above the minimum threshold are closed periodically to manage resources. Pruning helps reclaim leaked connections and maintain the pool size within the defined limits.
- **Warm-Up Process**: On application startup, the pool warms up to the minimum pool size by creating connections in the background.
- **Handling Broken Connections**: Broken connections are detected and handled by creating new connections if the session cannot be recovered.
- **Concurrency and Async Handling**: Connection creation should happen on separate threads to avoid queuing and improve performance. 
- **Security Considerations**: Pools are separated based on user authentication to prevent unauthorized access. 