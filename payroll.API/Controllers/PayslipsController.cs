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
            _connectionString = config.GetConnectionString("DefaultConnection");
            QuestPDF.Settings.License = LicenseType.Community;
        }

        // Action for Button: Save & Generate (Updates DB as Draft)
        [HttpPost("save")]
        public async Task<IActionResult> SavePayslip([FromBody] PayslipModel slip)
        {
            if (slip == null) return BadRequest("Invalid payslip data.");
            
            slip.IsSent = false; // Mark as draft
            await InternalDatabaseSync(slip);
            
            return Ok(new { Message = "Draft Saved", NetPay = slip.NetPay });
        }

        // Action for Button: Send to Employee (Marks as Official + Sends Email)
        [HttpPost("send")]
        public async Task<IActionResult> SendPayslip([FromBody] PayslipModel slip)
        {
            if (slip == null) return BadRequest("Invalid payslip data.");

            slip.IsSent = true; // Mark as official for Employee App visibility
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

        // Action for Button: Export PDF/Print (View in Chrome)
        [HttpGet("view-browser/{name}/{period}")]
        public async Task<IActionResult> ViewInBrowser(string name, string period)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            var slip = await connection.QueryFirstOrDefaultAsync<PayslipModel>(
                "SELECT * FROM payslips WHERE employee_name = @name AND date_generated = @period",
                new { name, period });

            if (slip == null) return NotFound("Payslip not found. Please save it first.");

            double gross = slip.BasicPay + slip.OvertimePay + slip.PerfectAttendance + slip.Incentives + slip.RestDayPay;
            
            // If dtr_logs exist in DB, deserialize them for the PDF generator
            if (!string.IsNullOrEmpty(slip.dtr_logs))
            {
                slip.DtrLogs = JsonSerializer.Deserialize<List<DailyLog>>(slip.dtr_logs);
            }

            byte[] pdfBytes = GeneratePayslipPdf(slip, gross);
            return File(pdfBytes, "application/pdf");
        }

        private async Task InternalDatabaseSync(PayslipModel slip)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            double grossIncome = slip.BasicPay + slip.OvertimePay + slip.PerfectAttendance + slip.Incentives + slip.RestDayPay;
            double totalDed = slip.LateDeduction + slip.UndertimeDeduction + slip.AbsenceDeduction + slip.SSS + slip.PhilHealth + slip.PagIBIG + slip.CashAdvanceDeduction + slip.OthersDeduction;

            slip.Deductions = totalDed;
            slip.NetPay = grossIncome - totalDed;
            slip.dtr_logs = JsonSerializer.Serialize(slip.DtrLogs ?? new List<DailyLog>());

            await connection.OpenAsync();
            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                await connection.ExecuteAsync(
                    "DELETE FROM payslips WHERE employee_name = @EmployeeName AND date_generated = @DateGenerated",
                    new { slip.EmployeeName, slip.DateGenerated }, transaction);

                string sqlInsert = @"INSERT INTO payslips (employee_name, basis, hourly_rate, basic_pay, perfect_attendance, incentives, rest_day_pay, 
                                    absence_deduction, late_minutes, late_deduction, undertime_minutes, undertime_deduction, 
                                    overtime_hours, overtime_pay, others_deduction, deductions, net_pay, 
                                    sss, phil_health, pag_ibig, cash_advance_deduction, date_generated, dtr_logs, is_sent) 
                                    VALUES (@EmployeeName, @Basis, @HourlyRate, @BasicPay, @PerfectAttendance, @Incentives, @RestDayPay, 
                                    @AbsenceDeduction, @LateMinutes, @LateDeduction, @UndertimeMinutes, @UndertimeDeduction, 
                                    @OvertimeHours, @OvertimePay, @OthersDeduction, @Deductions, @NetPay, 
                                    @SSS, @PhilHealth, @PagIBIG, @CashAdvanceDeduction, @DateGenerated, @dtr_logs, @IsSent)";

                await connection.ExecuteAsync(sqlInsert, slip, transaction);
                await transaction.CommitAsync();
            }
            catch { await transaction.RollbackAsync(); throw; }
        }

        private byte[] GeneratePayslipPdf(PayslipModel slip, double gross)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(0); 
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));
                    page.Content().Column(col =>
                    {
                        col.Item().Background("#5b6f82").PaddingVertical(25).PaddingHorizontal(30).Row(row =>
                        {
                            row.RelativeItem().AlignMiddle().Text("Sekyur-Link Employee Payslip").FontSize(18).FontColor(Colors.White);
                            row.RelativeItem().AlignRight().Column(c =>
                            {
                                c.Item().AlignRight().Text(slip.EmployeeName ?? "Employee").FontSize(12).Bold().FontColor(Colors.White);
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
                                    c.Item().Element(e => AddRow(e, "Cash Advance", slip.CashAdvanceDeduction));
                                    c.Item().Element(e => AddRow(e, "Others", slip.OthersDeduction));
                                });
                            });
                            body.Item().PaddingTop(25).Background("#cfd8dc").Padding(10).Row(row =>
                            {
                                row.RelativeItem().Text($"GROSS INCOME: ₱{gross:N2}").Bold().FontSize(10);
                                row.RelativeItem().AlignRight().Text($"TOTAL DEDUCTION: {slip.Deductions:N2}").Bold().FontSize(10);
                            });
                            body.Item().PaddingTop(15).Column(c =>
                            {
                                c.Item().Text("NET PAY:").FontSize(9).FontColor(Colors.Grey.Medium);
                                c.Item().Text($"₱{slip.NetPay:N2}").FontSize(26).ExtraBold().FontColor("#1b2b65");
                            });
                            body.Item().PaddingTop(20).Column(c =>
                            {
                                c.Item().Text("DAILY ATTENDANCE LOGS").FontSize(11).Bold().FontColor("#1b2b65");
                                c.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(cols => { cols.RelativeColumn(3); cols.RelativeColumn(2); cols.RelativeColumn(2); cols.RelativeColumn(2); });
                                    table.Header(h => {
                                        h.Cell().PaddingBottom(8).BorderBottom(1).Text("Date").Bold().FontSize(9);
                                        h.Cell().PaddingBottom(8).BorderBottom(1).Text("Time In").Bold().FontSize(9);
                                        h.Cell().PaddingBottom(8).BorderBottom(1).Text("Time Out").Bold().FontSize(9);
                                        h.Cell().PaddingBottom(8).BorderBottom(1).AlignRight().Text("Remarks").Bold().FontSize(9);
                                    });
                                    foreach (var log in slip.DtrLogs ?? new List<DailyLog>()) {
                                        table.Cell().PaddingVertical(5).BorderBottom(0.5f).Text(log.Date ?? "").FontSize(9);
                                        table.Cell().PaddingVertical(5).BorderBottom(0.5f).Text(log.TimeIn ?? "").FontSize(9);
                                        table.Cell().PaddingVertical(5).BorderBottom(0.5f).Text(log.TimeOut ?? "").FontSize(9);
                                        table.Cell().PaddingVertical(5).BorderBottom(0.5f).AlignRight().Text(log.Remarks ?? "").FontSize(9);
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
            string senderName = _config["EmailSettings:SenderName"];
            string senderEmail = _config["EmailSettings:SenderEmail"];
            string appPassword = _config["EmailSettings:AppPassword"];

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
    }
}