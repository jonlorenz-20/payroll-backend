# Sekyur-Link Payroll System - Core Backend Infrastructure

###  RESTful APIs with ASP.NET Core & Docker Containerization

The robust, scalable, and dockerized enterprise core engine for the **Sekyur-Link Payroll System**. This repository hosts the high-performance **RESTful APIs built with ASP.NET Core**, responsible for orchestrating secure database transactions, computing complex employee salary matrix schemas, rendering dynamic PDF payroll bundles, and triggering automated SMTP email workflows.

The system is fully containerized to guarantee zero-configuration deployment states across any staging or live hosting environment using **Docker Desktop** and **Docker Compose**.

---

## ✨ Features

* **Containerized Database Operations:** Fully isolated PostgreSQL database tracking system configurations, master employee lists, and processed payout histories.
* **RESTful Controller Architecture:** Secure endpoints exposed seamlessly for seamless C# PascalCase data mapping serialization with .NET MAUI clients.
* **QuestPDF Dynamic Compilation Engine:** Rapid server-side PDF generation producing highly optimized payslip templates embedded with high-resolution graphics.
* **Automated SMTP Mail Dispatcher:** Direct interaction with Google Mail (SMTP) servers to safely dispatch encrypted financial document packets as secure attachments.
* **Swagger OpenAPI Testing Framework:** Native API documentation interface running cleanly in development layers to enable effortless diagnostic workflows.

---

## 🛠️ Tech Stack & Dependencies

* **Framework:** ASP.NET Core Web API (`net10.0`)
* **Database Engine:** PostgreSQL 16
* **Data Access Layer:** Dapper (High-Performance Micro-ORM) / Npgsql driver
* **Document Engine:** QuestPDF (Community License)
* **Mail Protocol Handler:** MailKit / MimeKit
* **Container Orchestration:** Docker / Docker Compose v3.8

---

## 📦 Container Services Layout

The infrastructure is orchestrating two primary node engines defined inside `docker-compose.yml`:

### 1. `payroll-postgres` (Database Tier)
* **Base Image:** `postgres:16`
* **Internal Routing Port:** `5432`
* **Host Access Port:** `5433` *(Used purely for developer inspection via tools like pgAdmin or DBeaver).*
* **Persistence Mechanism:** Connected to a named volume layer `pgdata` to prevent database wipes when containers spin down.

### 2. `payroll-api` (Application Logic Tier)
* **Base Engine:** Built dynamically from the localized `Dockerfile`.
* **Internal Routing Port:** `8080`
* **Host Access Port:** `5016` *(The main gateway used by MAUI clients and web browsers).*
* **Environment Configuration:** Forces `ASPNETCORE_ENVIRONMENT=Development` to make Swagger UI accessible inside container nodes.

---

## 🚀 How to Launch and Initialize the Backend Server

Ensure you have **Docker Desktop** installed and running on your main server machine before executing deployment processes.

### 1: Clone and Navigate to the Backend Path
Open your terminal (PowerShell or Bash) and change directories to the project root directory containing the `docker-compose.yml` file:

-powershell
cd C:\Users\admin\source\repos\payroll.API

---
### 2: Build and Run Containers
docker-compose up --build -d

### 3: Verify Operational Status
docker ps

---
Diagnostics & Endpoint Testing (Swagger)
Once the containers are running, you can cleanly run endpoints diagnostics directly without needing a frontend client connected.

Open Google Chrome and navigate directly to the following URL string to access the interactive controller matrix:
👉 http://localhost:5016/swagger/index.html

Active Target Routes for Integration
GET /api/Payslips - Pulls the master historical ledger array.

POST /api/Payslips/save - Stages draft entities without sending notifications.

POST /api/Payslips/send - Commits entries to the official ledger database and fires email triggers to employee mailboxes.

🛠️ Network Expansion & Production Configuration
1. Public IP Port Forwarding Setup
   
To allow external .apk mobile installations and remote office desktop clients to connect to this server:

1. Access your router gateway dashboard.
2. Direct an external rule map linking External Port 5016 to your host machine's Internal Local IP Address through Internal Port 5016 (Protocol: TCP).

2. Environment Variables Configuration
If you shift production environments or modify default credentials, update the target bindings under the environment: section inside docker-compose.yml:
YAML
environment:
  - ASPNETCORE_ENVIRONMENT=Development
  - ConnectionStrings__DefaultConnection=Server=db;Port=5432;Database=sekyurlink_payroll;User Id=postgres;Password=YOUR_NEW_SECURE_PASSWORD;


