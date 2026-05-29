-- 1. EMPLOYEES 
CREATE TABLE public.employees (
	id serial4 NOT NULL,
	biometric_id varchar(50) NULL,
	"name" varchar(100) NOT NULL,
	department varchar(50) NULL,
	basis varchar(50) NULL,
	rate numeric(10, 2) NULL,
	username varchar(50) NOT NULL,
	"password" varchar(50) NOT NULL,
	shift_schedule varchar(50) DEFAULT '7:00 AM - 4:00 PM'::character varying NULL,
	cash_advance_balance float8 DEFAULT 0 NULL,
	date_hired timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	email text NULL,
	CONSTRAINT employees_name_key UNIQUE (name),
	CONSTRAINT employees_pkey PRIMARY KEY (id),
	CONSTRAINT employees_username_key UNIQUE (username)
);

-- 2. DTR HISTORY
CREATE TABLE public.dtr_history (
	id serial4 NOT NULL,
	biometric_id varchar(50) NOT NULL,
	employee_name varchar(100) NOT NULL,
	cutoff_period varchar(50) NOT NULL,
	days_worked numeric(5, 2) DEFAULT 0 NULL,
	late_minutes numeric(10, 2) DEFAULT 0 NULL,
	undertime_minutes numeric(10, 2) DEFAULT 0 NULL,
	overtime_hours numeric(10, 2) DEFAULT 0 NULL,
	date_uploaded timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	CONSTRAINT dtr_history_pkey PRIMARY KEY (id)
);

-- 3. LEAVE REQUESTS
CREATE TABLE public.leave_requests (
	id serial4 NOT NULL,
	employee_name text NOT NULL,
	leave_type text NOT NULL,
	date_requested text NOT NULL,
	status text DEFAULT 'Pending'::text NULL,
	CONSTRAINT leave_requests_pkey PRIMARY KEY (id)
);

-- 4. PAYSLIPS 
CREATE TABLE public.payslips (
	id serial4 NOT NULL,
	employee_name varchar(100) NULL,
	basis varchar(50) NULL,
	basic_pay numeric(10, 2) NULL,
	absence_deduction numeric(10, 2) NULL,
	undertime_minutes numeric(10, 2) NULL,
	undertime_deduction numeric(10, 2) NULL,
	overtime_pay numeric(10, 2) NULL,
	deductions numeric(10, 2) NULL,
	net_pay numeric(10, 2) NULL,
	sss numeric(10, 2) NULL,
	phil_health numeric(10, 2) NULL,
	pag_ibig numeric(10, 2) NULL,
	date_generated varchar(100) NULL,
	dtr_logs text NULL,
	hourly_rate float8 DEFAULT 0 NULL,
	late_minutes float8 DEFAULT 0 NULL,
	late_deduction float8 DEFAULT 0 NULL,
	overtime_hours float8 DEFAULT 0 NULL,
	cash_advance_deduction float8 DEFAULT 0 NULL,
	others_deduction float8 DEFAULT 0 NULL,
	incentives float8 DEFAULT 0 NULL,
	rest_day_pay float8 DEFAULT 0 NULL,
	perfect_attendance float8 DEFAULT 0 NULL,
	CONSTRAINT payslips_pkey PRIMARY KEY (id),
	CONSTRAINT payslips_employee_name_fkey FOREIGN KEY (employee_name) REFERENCES public.employees("name") ON UPDATE CASCADE
);



-- 5. INITIAL ADMIN ACCOUNT
INSERT INTO public.employees (biometric_id, "name", department, basis, rate, username, "password")
VALUES ('1001', 'Administrator', 'IT', 'Monthly', 0, 'admin', 'admin123')
ON CONFLICT (username) DO NOTHING;