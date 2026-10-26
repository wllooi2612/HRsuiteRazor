-- =======================================================================
-- HRsuite MySQL Performance Index Tuning Script
-- Database: myhrsuitelog
-- Compatible with MySQL 5.7+ / MySQL 8.0+ / MariaDB
-- =======================================================================

USE `myhrsuitelog`;

-- -------------------------------------------------------------
-- 1. Table `log` (High-Volume Audit Logs)
-- -------------------------------------------------------------

-- Range Scan & Reverse Chronological Ordering (Eliminates filesort)
ALTER TABLE `log` ADD INDEX `idx_log_date` (`Date`, `Id`);

-- Filter by Log Level + Date (e.g. ERROR audits)
ALTER TABLE `log` ADD INDEX `idx_log_level_date` (`Level`, `Date`);

-- Filter by User ID + Date (e.g. User activity tracking)
ALTER TABLE `log` ADD INDEX `idx_log_userid_date` (`UserID`, `Date`);

-- Filter by Page Name + Date
ALTER TABLE `log` ADD INDEX `idx_log_pagename_date` (`PageName`, `Date`);

-- Full-Text Index on Message (Eliminates O(N) full table scans with LIKE '%...%')
ALTER TABLE `log` ADD FULLTEXT INDEX `ft_log_message` (`Message`);


-- -------------------------------------------------------------
-- 2. Table `aspnet_useraccessright` (Permissions & Dynamic Navigation)
-- -------------------------------------------------------------

-- Covering Index for user permissions checks (Reduces 4,000+ row table scan to 1-row index lookup)
ALTER TABLE `aspnet_useraccessright` ADD INDEX `idx_useraccess_user_group` (`UserID`, `GroupID2`, `AccessRight`, `GroupCode`);


-- -------------------------------------------------------------
-- 3. Table `aspnet_users` (Authentication & Profile)
-- -------------------------------------------------------------

-- Unique index for username lookup during login and credential verification
ALTER TABLE `aspnet_users` ADD UNIQUE INDEX `idx_users_name` (`Name`);


-- -------------------------------------------------------------
-- 4. Transactions: `aspnet_leaverecord` & `aspnet_otrecord`
-- -------------------------------------------------------------

-- Employee-specific transaction history & approval lookups
ALTER TABLE `aspnet_leaverecord` ADD INDEX `idx_leaverecord_empid` (`EMP_ID`, `LEAVE_DATE`);
ALTER TABLE `aspnet_otrecord` ADD INDEX `idx_otrecord_empid` (`EMP_ID`, `OT_DATE`);
