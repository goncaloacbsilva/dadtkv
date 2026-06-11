# DADTKV

DADTKV is a fault-tolerant distributed transactional key-value store developed in C#. The system provides **strict serializability** through a combination of **lease-based concurrency control**, **Paxos consensus**, and **replicated transaction managers**.

The architecture is composed of three tiers:

- **Clients** that execute transactional workloads.
- **Transaction Managers** that process transactions and replicate updates.
- **Lease Managers** that coordinate data ownership using the Paxos consensus algorithm.

By assigning leases to transaction managers, DADTKV allows non-conflicting transactions to execute concurrently while ensuring consistency for conflicting operations. The system is designed to tolerate process crashes, maintain replicated state across nodes, and guarantee durable transaction execution.

## Features

- Distributed transactional key-value store
- Strict serializability guarantees
- Lease-based concurrency control
- Paxos-based lease assignment and consensus
- Replicated transaction managers
- Crash fault tolerance
- gRPC-based communication
- In-memory data storage
- Distributed transaction processing

## Technologies

- .NET
- gRPC
- Paxos Consensus Algorithm

## Architecture

```text
+---------+        +----------------------+        +------------------+
| Clients | <----> | Transaction Managers | <----> | Lease Managers   |
+---------+        +----------------------+        +------------------+
                             |                             |
                             +------ Replication ----------+
```

## Academic Context

This project was developed for the **Design and Implementation of Distributed Applications (DAD)** course at **Instituto Superior Técnico (IST)**, focusing on distributed systems, consensus protocols, fault tolerance, and transactional data management.
