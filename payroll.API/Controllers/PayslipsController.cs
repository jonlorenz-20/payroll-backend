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
        private readonly IConfiguration _config; // <-- STEP 2: Idinagdag para mabasa ang appsettings.json

        public PayslipsController(IConfiguration config)
        {
            _config = config; // <-- STEP 2: In-assign ang config
            _connectionString = config.GetConnectionString("DefaultConnection");
            // Setup License for QuestPDF
            QuestPDF.Settings.License = LicenseType.Community;
        }

        [HttpPost("save")]
        public async Task<IActionResult> SavePayslip([FromBody] PayslipModel slip)
        {
            if (slip == null) return BadRequest("Invalid payslip data.");

            using var connection = new NpgsqlConnection(_connectionString);

            // 1. CALCULATIONS
            double grossIncome = slip.BasicPay + slip.OvertimePay + slip.PerfectAttendance + slip.Incentives + slip.RestDayPay;
            double totalDed = slip.LateDeduction + slip.UndertimeDeduction + slip.AbsenceDeduction + slip.SSS + slip.PhilHealth + slip.PagIBIG + slip.CashAdvanceDeduction + slip.OthersDeduction;

            slip.Deductions = totalDed;
            slip.NetPay = grossIncome - totalDed;

            var logsToSave = slip.DtrLogs ?? new List<DailyLog>();
            slip.dtr_logs = JsonSerializer.Serialize(logsToSave);

            try
            {
                await connection.OpenAsync();
                using var transaction = await connection.BeginTransactionAsync();

                try
                {
                    // 2. DATABASE SYNC
                    await connection.ExecuteAsync(
                        "DELETE FROM payslips WHERE employee_name = @EmployeeName AND date_generated = @DateGenerated",
                        new { slip.EmployeeName, slip.DateGenerated },
                        transaction);

                    string sqlInsert = @"INSERT INTO payslips (employee_name, basis, hourly_rate, basic_pay, perfect_attendance, incentives, rest_day_pay, 
                                        absence_deduction, late_minutes, late_deduction, undertime_minutes, undertime_deduction, 
                                        overtime_hours, overtime_pay, others_deduction, deductions, net_pay, 
                                        sss, phil_health, pag_ibig, cash_advance_deduction, date_generated, dtr_logs) 
                                        VALUES (@EmployeeName, @Basis, @HourlyRate, @BasicPay, @PerfectAttendance, @Incentives, @RestDayPay, 
                                        @AbsenceDeduction, @LateMinutes, @LateDeduction, @UndertimeMinutes, @UndertimeDeduction, 
                                        @OvertimeHours, @OvertimePay, @OthersDeduction, @Deductions, @NetPay, 
                                        @SSS, @PhilHealth, @PagIBIG, @CashAdvanceDeduction, @DateGenerated, @dtr_logs)";

                    await connection.ExecuteAsync(sqlInsert, slip, transaction);
                    await transaction.CommitAsync();
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }

                // 3. EMAIL & PDF GENERATION
                var emp = await connection.QueryFirstOrDefaultAsync<EmployeeModel>(
                    "SELECT * FROM employees WHERE name = @Name",
                    new { Name = slip.EmployeeName });

                if (emp != null && !string.IsNullOrEmpty(emp.Email))
                {
                    byte[] pdfBytes = GeneratePayslipPdf(slip, grossIncome);
                    await SendEmailWithAttachment(emp.Email, slip, pdfBytes);
                }

                return Ok(new { Message = "Saved and PDF Sent", NetPay = slip.NetPay });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"API Error: {ex.Message}");
                return StatusCode(500, $"Internal Error: {ex.Message}");
            }
        }

        // ==========================================================
        // EXACT MATCH PDF DESIGN LOGIC
        // ==========================================================
        private byte[] GeneratePayslipPdf(PayslipModel slip, double gross)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(0); // Set sa 0 para yung header banner ay full width
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                    page.Content().Column(col =>
                    {
                        // HEADER BANNER
                        col.Item().Background("#5b6f82").PaddingVertical(25).PaddingHorizontal(30).Row(row =>
                        {
                            row.RelativeItem().AlignMiddle().Text("Sekyur-Link Employee Payslip").FontSize(18).FontColor(Colors.White);
                            row.RelativeItem().AlignRight().Column(c =>
                            {
                                c.Item().AlignRight().Text(slip.EmployeeName ?? "Employee").FontSize(12).Bold().FontColor(Colors.White);
                                c.Item().AlignRight().Text(slip.DateGenerated ?? "N/A").FontSize(9).FontColor(Colors.White);
                            });
                        });

                        // BODY CONTENT
                        col.Item().PaddingHorizontal(30).PaddingTop(20).Column(body =>
                        {
                            // TWO COLUMNS (Earnings at Deductions)
                            body.Item().Row(row =>
                            {
                                // EARNINGS COLUMN
                                row.RelativeItem().PaddingRight(15).Column(c =>
                                {
                                    c.Item().PaddingBottom(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                     .Text("EARNINGS & ALLOWANCES").FontSize(10).Bold().FontColor("#1b2b65");

                                    c.Item().PaddingTop(10).Element(e => AddRow(e, "Base Basic Pay", slip.BasicPay));
                                    c.Item().Element(e => AddRow(e, "Overtime Pay", slip.OvertimePay));
                                    c.Item().Element(e => AddRow(e, "Perfect Attendance", slip.PerfectAttendance));
                                    c.Item().Element(e => AddRow(e, "Incentives", slip.Incentives));
                                    c.Item().Element(e => AddRow(e, "Rest Day Pay", slip.RestDayPay));
                                });

                                // DEDUCTIONS COLUMN
                                row.RelativeItem().PaddingLeft(15).Column(c =>
                                {
                                    c.Item().PaddingBottom(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                     .Text("DEDUCTIONS").FontSize(10).Bold().FontColor("#1b2b65");

                                    double combinedLates = slip.LateDeduction + slip.UndertimeDeduction;

                                    c.Item().PaddingTop(10).Element(e => AddRow(e, "Late / Undertime", combinedLates, true));
                                    c.Item().Element(e => AddRow(e, "Absences", slip.AbsenceDeduction, true));
                                    c.Item().Element(e => AddRow(e, "SSS", slip.SSS));
                                    c.Item().Element(e => AddRow(e, "PhilHealth", slip.PhilHealth));
                                    c.Item().Element(e => AddRow(e, "Pag-IBIG", slip.PagIBIG));
                                    c.Item().Element(e => AddRow(e, "Cash Advance (Bale)", slip.CashAdvanceDeduction));
                                    c.Item().Element(e => AddRow(e, "Others", slip.OthersDeduction));

                                    c.Item().PaddingTop(15).BorderTop(1).BorderColor(Colors.Grey.Lighten2).PaddingTop(5)
                                     .Element(e => AddRow(e, "Total Ded.", slip.Deductions));
                                });
                            });

                            // SUMMARY BAR
                            body.Item().PaddingTop(25).Background("#cfd8dc").Padding(10).Row(row =>
                            {
                                row.RelativeItem().Text($"GROSS INCOME: ₱{gross:N2}").Bold().FontSize(10).FontColor(Colors.Black);
                                row.RelativeItem().AlignRight().Text($"TOTAL DEDUCTION: {slip.Deductions:N2}").Bold().FontSize(10).FontColor(Colors.Black);
                            });

                            // NET PAY
                            body.Item().PaddingTop(15).Column(c =>
                            {
                                c.Item().Text("NET PAY:").FontSize(9).FontColor(Colors.Grey.Medium);
                                c.Item().Text($"₱{slip.NetPay:N2}").FontSize(26).ExtraBold().FontColor("#1b2b65");
                                c.Item().Text("(System Generated Document)").FontSize(9).Italic().FontColor(Colors.Grey.Medium);
                            });

                            // ORANGE SEPARATOR LINE
                            body.Item().PaddingTop(15).LineHorizontal(2).LineColor("#f39c12");

                            // DAILY ATTENDANCE LOGS
                            body.Item().PaddingTop(20).Column(c =>
                            {
                                c.Item().Text("DAILY ATTENDANCE LOGS").FontSize(11).Bold().FontColor("#1b2b65");
                                c.Item().PaddingBottom(15).Text("Based on uploaded Biometrics DTR").FontSize(9).FontColor(Colors.Grey.Medium);

                                c.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(cols =>
                                    {
                                        cols.RelativeColumn(3); // Date
                                        cols.RelativeColumn(2); // Time In
                                        cols.RelativeColumn(2); // Time Out
                                        cols.RelativeColumn(2); // Remarks
                                    });

                                    table.Header(h =>
                                    {
                                        h.Cell().PaddingBottom(8).BorderBottom(1).BorderColor(Colors.Grey.Medium).Text("Date").Bold().FontSize(9);
                                        h.Cell().PaddingBottom(8).BorderBottom(1).BorderColor(Colors.Grey.Medium).Text("Time In").Bold().FontSize(9);
                                        h.Cell().PaddingBottom(8).BorderBottom(1).BorderColor(Colors.Grey.Medium).Text("Time Out").Bold().FontSize(9);
                                        h.Cell().PaddingBottom(8).BorderBottom(1).BorderColor(Colors.Grey.Medium).AlignRight().Text("Remarks").Bold().FontSize(9);
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

        // HELPER METHOD PARA MAPADALI ANG PAGGAWA NG ROWS
        private void AddRow(IContainer container, string label, double value, bool isRed = false)
        {
            container.PaddingVertical(3).Row(r =>
            {
                r.RelativeItem().Text(label).FontSize(10).FontColor(isRed ? "#c0392b" : Colors.Black);
                r.RelativeItem().AlignRight().Text(value.ToString("N2")).FontSize(10).FontColor(Colors.Black);
            });
        }

        private async Task SendEmailWithAttachment(string email, PayslipModel slip, byte[] pdfBytes)
        {
            // <-- STEP 3: Kukunin na natin yung tinago mong credentials sa appsettings.json
            string senderName = _config["EmailSettings:SenderName"];
            string senderEmail = _config["EmailSettings:SenderEmail"];
            string appPassword = _config["EmailSettings:AppPassword"];

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(senderName, senderEmail)); // Gumagamit na ng variables
            message.To.Add(new MailboxAddress(slip.EmployeeName, email));
            message.Subject = $"Official Payslip: {slip.DateGenerated}";

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = $"<h3>Hi {slip.EmployeeName},</h3><p>Attached is your official payslip for the period <b>{slip.DateGenerated}</b>.</p><p>Thank you!</p>"
            };

            bodyBuilder.Attachments.Add($"Payslip_{slip.EmployeeName.Replace(" ", "_")}.pdf", pdfBytes, new ContentType("application", "pdf"));
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            try
            {
                await client.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(senderEmail, appPassword); // Naka-hide na ang password mo dito!
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Email Error: {ex.Message}");
            }
        }
    }
}