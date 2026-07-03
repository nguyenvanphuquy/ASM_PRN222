namespace ServiceLayer.Dtos;

/// <summary>Số dư token của một người dùng (tổng cấp / đã dùng / còn lại).</summary>
public record TokenBalance(int Granted, int Used, int Remaining, DateTime? NextExpiry);
