"""A Python implementation of the tool that verifies whether a badge swipe is valid
Authorizes and logs a reset based on the permissions stored in the DB
"""
from dataclasses import dataclass
import pyodbc
import sys
import os
from typing import Optional
# from dotenv import load_dotenv

@dataclass
class ResetAttempt:
    """A DTO containing the information to log for a reset attempt"""
    associate_num: int
    associate_name: str
    cmms_num: int
    line_name: str
    is_authorized: bool
    
def main():
    """Entry point for the authorization class"""
    if(len(sys.argv) < 3):
        print("Usage: python AuthorizeReset [badge number] [CMMS number]")
        return
    
    try:
        badge_num = int(sys.argv[1])
        cmms_num = int(sys.argv[2])
    except ValueError:
        print("Please ensure both badge number and CMMS number are whole numbers")
        return

    # load_dotenv()
    conn_str = (
        f"DRIVER={{ODBC Driver 17 for SQL Server}};"
        f"SERVER={os.getenv('DB_SERVER')};"
        f"DATABASE={os.getenv('DB_NAME')};"
        f"UID={os.getenv('DB_USER')};"
        f"PWD={os.getenv('DB_PASS')};"
        "TrustServerCertificate=yes;" #TODO insecure, eventually require certificate verification
    )

    try:
        with pyodbc.connect(conn_str) as conn:
            attempt = authorize(badge_num, cmms_num, conn)
            
            if attempt:
                log_reset_attempt(attempt, conn)
                print(f"Authorized: {attempt.is_authorized}")
            else:
                # Terminal coloring via ANSI codes
                print("\033[91mERROR: Invalid Badge or CMMS number.\033[0m")
    except Exception as e:
        print(f"Database Error: {e}")

def authorize(badge_num: int, cmms_num: int, conn) -> Optional[ResetAttempt]:
    """
    Authorize a badge swipe to release a certain machine and collects the request data

    Args:
        badgeNum (int): The badge number read from the badge reader
        cmmsNum (int): The machine's CMMS number
        conn (Connection): The open SQL connection
    
    Returns:
        Null if badge/CMMS does not exist,
        otherwise a ResetAttempt dataclass containing associate name, number, CMMS, line name, and whether the request was authorized
    """
    # 1. Lookup associate by badge (PK on badgeNum - fast)
    # 2. Check if CMMS maps to one of those lines (indexed on lineName)
    # 3. Get lines for that associate (indexed on associateNum)
    sql = """
    SELECT TOP 1 a.associateNum, a.associateName, ctl.lineName,
    CAST(CASE WHEN atl.associateNum IS NOT NULL THEN 1 ELSE 0 END AS BIT) as IsAuthorized
    FROM AssociateInfo a
    INNER JOIN CmmsToLineName ctl ON ctl.cmmsNum = ?
    LEFT JOIN AssociateToLine atl ON a.associateNum = atl.associateNum AND ctl.lineName = atl.lineName
    WHERE a.badgeNum = ?"""

    # Set up a reader to build a record from the returned data
    with conn.cursor() as cursor:
        cursor.execute(sql, (cmms_num, badge_num))
        row = cursor.fetchone()

    if row:
        return ResetAttempt(
            associate_num=row[0],
            associate_name=row[1],
            cmms_num=cmms_num,
            line_name=row[2],
            is_authorized=bool(row[3])
        )
    return None # the badge or CMMS doesn't exist

def log_reset_attempt(attempt: ResetAttempt, conn):
    """
    Logs an attempted reset in the historical database

    Args:
        attempt (ResetAttempt) The ResetAttempt record to log
        conn(Connection) The open SQL connection
    """
    # Authorize already did the heavy lifting of getting the data to insert
    sql = """
        INSERT INTO Historical (requestTime, associateNum, associateName, cmmsNum, lineName, isAuthorized)
        VALUES (GETDATE(), ?, ?, ?, ?, ?)"""

    with conn.cursor() as cursor:
        # Get the parameters for the SQL statement from the DTO
        cursor.execute(sql, (
            attempt.associate_num, 
            attempt.associate_name, 
            attempt.cmms_num, 
            attempt.line_name, 
            attempt.is_authorized
        ))
        conn.commit()

if __name__ == "__main__":
    main()