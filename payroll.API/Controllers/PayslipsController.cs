using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Dapper;
using payroll.API.Models;
using System.Text.Json;
using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace payroll.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PayslipsController : ControllerBase
    {
        private readonly string _connectionString;
        private readonly IConfiguration _config;

        public PayslipsController(IConfiguration config)
        {
            _config = config;
            _connectionString = config.GetConnectionString("DefaultConnection")!;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        // --- GET ALL PAYSLIPS ---
        [HttpGet]
        public async Task<IActionResult> GetPayslips()
        {
            using var connection = new NpgsqlConnection(_connectionString);

            string sqlSelectAll = @"SELECT 
                                        employee_name AS EmployeeName, 
                                        basis AS Basis, 
                                        hourly_rate AS HourlyRate, 
                                        basic_pay AS BasicPay, 
                                        perfect_attendance AS PerfectAttendance, 
                                        incentives AS Incentives, 
                                        rest_day_pay AS RestDayPay, 
                                        absence_deduction AS AbsenceDeduction, 
                                        late_minutes AS LateMinutes, 
                                        late_deduction AS LateDeduction, 
                                        undertime_minutes AS UndertimeMinutes, 
                                        undertime_deduction AS UndertimeDeduction, 
                                        overtime_hours AS OvertimeHours, 
                                        overtime_pay AS OvertimePay, 
                                        others_deduction AS OthersDeduction, 
                                        deductions AS Deductions, 
                                        net_pay AS NetPay, 
                                        sss AS SSS, 
                                        phil_health AS PhilHealth, 
                                        pag_ibig AS PagIBIG, 
                                        cash_advance_deduction AS CashAdvanceDeduction, 
                                        date_generated AS DateGenerated, 
                                        dtr_logs AS dtr_logs, 
                                        is_sent AS IsSent,
                                        position AS Position
                                    FROM payslips";

            var result = await connection.QueryAsync<PayslipModel>(sqlSelectAll);
            return Ok(result.ToList());
        }

        // --- BUTTON 1: SAVE & GENERATE (Draft Only) ---
        [HttpPost("save")]
        public async Task<IActionResult> SavePayslip([FromBody] PayslipModel slip)
        {
            if (slip == null) return BadRequest("Invalid payslip data.");

            slip.IsSent = false;
            await InternalDatabaseSync(slip);

            return Ok(new { Message = "Draft Saved", NetPay = slip.NetPay });
        }

        // --- BUTTON 3: SEND TO EMPLOYEE ---
        [HttpPost("send")]
        public async Task<IActionResult> SendPayslip([FromBody] PayslipModel slip)
        {
            if (slip == null) return BadRequest("Invalid payslip data.");

            slip.IsSent = true;
            await InternalDatabaseSync(slip);

            using var connection = new NpgsqlConnection(_connectionString);
            var emp = await connection.QueryFirstOrDefaultAsync<EmployeeModel>(
                "SELECT * FROM employees WHERE name = @Name",
                new { Name = slip.EmployeeName });

            if (emp != null && !string.IsNullOrEmpty(emp.Email))
            {
                double grossIncome = slip.BasicPay + slip.OvertimePay + slip.PerfectAttendance + slip.Incentives + slip.RestDayPay;
                byte[] pdfBytes = GeneratePayslipPdf(slip, grossIncome);
                await SendEmailWithAttachment(emp.Email, slip, pdfBytes);
            }

            return Ok(new { Message = "Official Payslip Sent" });
        }

        // --- BUTTON 2: EXPORT/PRINT ---
        [HttpGet("view-browser/{name}/{period}")]
        public async Task<IActionResult> ViewInBrowser(string name, string period)
        {
            string decodedName = Uri.UnescapeDataString(name);
            string decodedPeriod = Uri.UnescapeDataString(period);

            using var connection = new NpgsqlConnection(_connectionString);

            var row = await connection.QueryFirstOrDefaultAsync(
                @"SELECT employee_name, basis, hourly_rate, basic_pay, perfect_attendance, incentives, rest_day_pay, 
                         absence_deduction, late_minutes, late_deduction, undertime_minutes, undertime_deduction, 
                         overtime_hours, overtime_pay, others_deduction, deductions, net_pay, 
                         sss, phil_health, pag_ibig, cash_advance_deduction, date_generated, dtr_logs, position 
                  FROM payslips 
                  WHERE employee_name = @EmployeeName AND date_generated = @DateGenerated",
                new { EmployeeName = decodedName, DateGenerated = decodedPeriod });

            if (row == null) return NotFound("Payslip record not found in database.");

            var slip = new PayslipModel
            {
                EmployeeName = row.employee_name,
                Basis = row.basis,
                HourlyRate = Convert.ToDouble(row.hourly_rate ?? 0),
                BasicPay = Convert.ToDouble(row.basic_pay ?? 0),
                PerfectAttendance = Convert.ToDouble(row.perfect_attendance ?? 0),
                Incentives = Convert.ToDouble(row.incentives ?? 0),
                RestDayPay = Convert.ToDouble(row.rest_day_pay ?? 0),
                AbsenceDeduction = Convert.ToDouble(row.absence_deduction ?? 0),
                LateMinutes = Convert.ToDouble(row.late_minutes ?? 0),
                LateDeduction = Convert.ToDouble(row.late_deduction ?? 0),
                UndertimeMinutes = Convert.ToDouble(row.undertime_minutes ?? 0),
                UndertimeDeduction = Convert.ToDouble(row.undertime_deduction ?? 0),
                OvertimeHours = Convert.ToDouble(row.overtime_hours ?? 0),
                OvertimePay = Convert.ToDouble(row.overtime_pay ?? 0),
                OthersDeduction = Convert.ToDouble(row.others_deduction ?? 0),
                Deductions = Convert.ToDouble(row.deductions ?? 0),
                NetPay = Convert.ToDouble(row.net_pay ?? 0),
                SSS = Convert.ToDouble(row.sss ?? 0),
                PhilHealth = Convert.ToDouble(row.phil_health ?? 0),
                PagIBIG = Convert.ToDouble(row.pag_ibig ?? 0),
                CashAdvanceDeduction = Convert.ToDouble(row.cash_advance_deduction ?? 0),
                DateGenerated = row.date_generated,
                Position = row.position ?? "Staff"
            };

            string logsJson = row.dtr_logs;
            if (!string.IsNullOrEmpty(logsJson))
            {
                slip.DtrLogs = JsonSerializer.Deserialize<List<DailyLog>>(logsJson);
            }

            double gross = slip.BasicPay + slip.OvertimePay + slip.PerfectAttendance + slip.Incentives + slip.RestDayPay;

            byte[] pdfBytes = GeneratePayslipPdf(slip, gross);
            return File(pdfBytes, "application/pdf");
        }

        // 🎯 BULLETPROOF DATABASE SYNC: Selyadong isinusulat ang string DTR Logs sa database
        private async Task InternalDatabaseSync(PayslipModel slip)
        {
            using var connection = new NpgsqlConnection(_connectionString);

            double grossIncome = slip.BasicPay + slip.OvertimePay + slip.PerfectAttendance + slip.Incentives + slip.RestDayPay;
            double totalDed = slip.LateDeduction + slip.UndertimeDeduction + slip.AbsenceDeduction + slip.SSS + slip.PhilHealth + slip.PagIBIG + slip.CashAdvanceDeduction + slip.OthersDeduction;

            slip.Deductions = totalDed;
            slip.NetPay = grossIncome - totalDed;

            // Priority: Kung may string na ipinasa ang MAUI app (dtr_logs), yun ang gagamitin natin
            string finalDtrString = "[]";
            if (!string.IsNullOrEmpty(slip.dtr_logs) && slip.dtr_logs != "null")
            {
                finalDtrString = slip.dtr_logs;
            }
            else if (slip.DtrLogs != null && slip.DtrLogs.Count > 0)
            {
                finalDtrString = JsonSerializer.Serialize(slip.DtrLogs);
            }

            await connection.OpenAsync();
            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                await connection.ExecuteAsync(
                    "DELETE FROM payslips WHERE employee_name = @EmployeeName AND date_generated = @DateGenerated",
                    new { EmployeeName = slip.EmployeeName, DateGenerated = slip.DateGenerated }, transaction);

                string sqlInsert = @"INSERT INTO payslips (employee_name, basis, hourly_rate, basic_pay, perfect_attendance, incentives, rest_day_pay, 
                                    absence_deduction, late_minutes, late_deduction, undertime_minutes, undertime_deduction, 
                                    overtime_hours, overtime_pay, others_deduction, deductions, net_pay, 
                                    sss, phil_health, pag_ibig, cash_advance_deduction, date_generated, dtr_logs, is_sent, position) 
                                    VALUES (@EmpName, @Basis, @HourlyRate, @BasicPay, @PerfectAttendance, @Incentives, @RestDayPay, 
                                    @AbsenceDed, @LateMins, @LateDed, @UtMins, @UtDed, 
                                    @OtHours, @OtPay, @OthersDed, @Deductions, @NetPay, 
                                    @SSS, @PhilHealth, @PagIBIG, @CashAdvanceDed, @DateGen, @DtrLogsStr, @IsSent, @Position)";

                await connection.ExecuteAsync(sqlInsert, new
                {
                    EmpName = slip.EmployeeName,
                    Basis = slip.Basis,
                    HourlyRate = slip.HourlyRate,
                    BasicPay = slip.BasicPay,
                    PerfectAttendance = slip.PerfectAttendance,
                    Incentives = slip.Incentives,
                    RestDayPay = slip.RestDayPay,
                    AbsenceDed = slip.AbsenceDeduction,
                    LateMins = slip.LateMinutes,
                    LateDed = slip.LateDeduction,
                    UtMins = slip.UndertimeMinutes,
                    UtDed = slip.UndertimeDeduction,
                    OtHours = slip.OvertimeHours,
                    OtPay = slip.OvertimePay,
                    OthersDed = slip.OthersDeduction,
                    Deductions = slip.Deductions,
                    NetPay = slip.NetPay,
                    SSS = slip.SSS,
                    PhilHealth = slip.PhilHealth,
                    PagIBIG = slip.PagIBIG,
                    CashAdvanceDed = slip.CashAdvanceDeduction,
                    DateGen = slip.DateGenerated,
                    DtrLogsStr = finalDtrString, // SIGURADONG MAY LAMAN ITO
                    IsSent = slip.IsSent,
                    Position = slip.Position ?? "Staff"
                }, transaction);

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private byte[] GeneratePayslipPdf(PayslipModel slip, double gross)
        {
            string assetFolder = Path.Combine(AppContext.BaseDirectory, "Assets");
            string logoPath = Path.Combine(assetFolder, "sekyur_logo.png");

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(0);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                    page.Content().Column(col =>
                    {
                        col.Item().Background("#5b6f82").PaddingVertical(15).PaddingHorizontal(30).Row(row =>
                        {
                            if (System.IO.File.Exists(logoPath))
                            {
                                row.ConstantItem(95).AlignMiddle().Image(logoPath).FitArea();
                                row.ConstantItem(15);
                            }

                            row.RelativeItem().AlignMiddle().Text("Sekyur-Link Employee Payslip").FontSize(14).FontColor(Colors.White);
                            row.RelativeItem().AlignRight().Column(c =>
                            {
                                c.Item().AlignRight().Text(slip.EmployeeName ?? "Employee").FontSize(12).Bold().FontColor(Colors.White);
                                c.Item().AlignRight().Text($"Designation: {slip.Position ?? "Staff"}").FontSize(10).FontColor(Colors.White).Italic();
                                c.Item().AlignRight().Text(slip.DateGenerated ?? "N/A").FontSize(9).FontColor(Colors.White);
                            });
                        });

                        col.Item().PaddingHorizontal(30).PaddingTop(20).Column(body =>
                        {
                            body.Item().Row(row =>
                            {
                                row.RelativeItem().PaddingRight(15).Column(c =>
                                {
                                    c.Item().PaddingBottom(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Text("EARNINGS & ALLOWANCES").FontSize(10).Bold().FontColor("#1b2b65");
                                    c.Item().PaddingTop(10).Element(e => AddRow(e, "Base Basic Pay", slip.BasicPay));
                                    c.Item().Element(e => AddRow(e, "Overtime Pay", slip.OvertimePay));
                                    c.Item().Element(e => AddRow(e, "Perfect Attendance", slip.PerfectAttendance));
                                    c.Item().Element(e => AddRow(e, "Incentives", slip.Incentives));
                                    c.Item().Element(e => AddRow(e, "Rest Day Pay", slip.RestDayPay));
                                });

                                row.RelativeItem().PaddingLeft(15).Column(c =>
                                {
                                    c.Item().PaddingBottom(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Text("DEDUCTIONS").FontSize(10).Bold().FontColor("#1b2b65");
                                    c.Item().PaddingTop(10).Element(e => AddRow(e, "Late / Undertime", slip.LateDeduction + slip.UndertimeDeduction, true));
                                    c.Item().Element(e => AddRow(e, "Absences", slip.AbsenceDeduction, true));
                                    c.Item().Element(e => AddRow(e, "SSS", slip.SSS));
                                    c.Item().Element(e => AddRow(e, "PhilHealth", slip.PhilHealth));
                                    c.Item().Element(e => AddRow(e, "Pag-IBIG", slip.PagIBIG));
                                    c.Item().Element(e => AddRow(e, "Cash Advance (Bale)", slip.CashAdvanceDeduction));
                                    c.Item().Element(e => AddRow(e, "Others", slip.OthersDeduction));
                                    c.Item().PaddingTop(15).BorderTop(1).BorderColor(Colors.Grey.Lighten2).PaddingTop(5).Element(e => AddRow(e, "Total Ded.", slip.Deductions));
                                });
                            });

                            body.Item().PaddingTop(25).Background("#cfd8dc").Padding(10).Row(row =>
                            {
                                row.RelativeItem().Text($"GROSS INCOME: ₱{gross:N2}").Bold().FontSize(10).FontColor(Colors.Black);
                                row.RelativeItem().AlignRight().Text($"TOTAL DEDUCTION: ₱{slip.Deductions:N2}").Bold().FontSize(10).FontColor(Colors.Black);
                            });

                            body.Item().PaddingTop(15).Column(c =>
                            {
                                c.Item().Text("NET PAY:").FontSize(9).FontColor(Colors.Grey.Medium);
                                c.Item().Text($"₱{slip.NetPay:N2}").FontSize(26).ExtraBold().FontColor("#1b2b65");
                                c.Item().Text("(System Generated Document)").FontSize(9).Italic().FontColor(Colors.Grey.Medium);
                            });

                            body.Item().PaddingTop(15).LineHorizontal(2).LineColor("#f39c12");

                            body.Item().PaddingTop(20).Column(c =>
                            {
                                c.Item().Text("DAILY ATTENDANCE LOGS").FontSize(11).Bold().FontColor("#1b2b65");
                                c.Item().PaddingBottom(15).Text("Based on uploaded Biometrics DTR").FontSize(9).FontColor(Colors.Grey.Medium);

                                c.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(cols => { cols.RelativeColumn(3); cols.RelativeColumn(2); cols.RelativeColumn(2); cols.RelativeColumn(2); });
                                    table.Header(h => {
                                        h.Cell().PaddingBottom(8).BorderBottom(1).Text("Date").Bold().FontSize(9);
                                        h.Cell().PaddingBottom(8).BorderBottom(1).Text("Time In").Bold().FontSize(9);
                                        h.Cell().PaddingBottom(8).BorderBottom(1).Text("Time Out").Bold().FontSize(9);
                                        h.Cell().PaddingBottom(8).BorderBottom(1).AlignRight().Text("Remarks").Bold().FontSize(9);
                                    });
                                    foreach (var log in slip.DtrLogs ?? new List<DailyLog>())
                                    {
                                        table.Cell().PaddingVertical(5).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Text(log.Date ?? "").FontSize(9);
                                        table.Cell().PaddingVertical(5).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Text(log.TimeIn ?? "").FontSize(9);
                                        table.Cell().PaddingVertical(5).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Text(log.TimeOut ?? "").FontSize(9);
                                        table.Cell().PaddingVertical(5).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).AlignRight().Text(log.Remarks ?? "").FontSize(9);
                                    }
                                });
                            });
                        });
                    });
                });
            }).GeneratePdf();
        }

        private void AddRow(IContainer container, string label, double value, bool isRed = false)
        {
            container.PaddingVertical(3).Row(r =>
            {
                r.RelativeItem().Text(label).FontSize(10).FontColor(isRed ? "#c0392b" : Colors.Black);
                r.RelativeItem().AlignRight().Text(value.ToString("N2")).FontSize(10);
            });
        }

        private async Task SendEmailWithAttachment(string email, PayslipModel slip, byte[] pdfBytes)
        {
            string senderName = _config["EmailSettings:SenderName"]!;
            string senderEmail = _config["EmailSettings:SenderEmail"]!;
            string appPassword = _config["EmailSettings:AppPassword"]!;

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(senderName, senderEmail));
            message.To.Add(new MailboxAddress(slip.EmployeeName, email));
            message.Subject = $"Official Payslip: {slip.DateGenerated}";

            var bodyBuilder = new BodyBuilder { HtmlBody = $"<h3>Hi {slip.EmployeeName},</h3><p>Attached is your official payslip for the period <b>{slip.DateGenerated}</b>.</p>" };
            bodyBuilder.Attachments.Add($"Payslip_{slip.EmployeeName.Replace(" ", "_")}.pdf", pdfBytes, new ContentType("application", "pdf"));
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(senderEmail, appPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }

        [HttpDelete("delete-draft/{name}/{period}")]
        public async Task<IActionResult> DeleteDraft(string name, string period)
        {
            string decodedName = Uri.UnescapeDataString(name);
            string decodedPeriod = Uri.UnescapeDataString(period);

            using var connection = new NpgsqlConnection(_connectionString);
            string sql = "DELETE FROM payslips WHERE employee_name = @Name AND date_generated = @Period AND is_sent = false";

            await connection.ExecuteAsync(sql, new { Name = decodedName, Period = decodedPeriod });
            return Ok(new { Message = "Draft deleted successfully" });
        }
    }
}