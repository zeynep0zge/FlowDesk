using FlowDesk.Data;
using FlowDesk.Models;
using FlowDesk.ViewModels.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.ViewComponents;

public sealed class FeedbackNotificationsViewComponent : ViewComponent
{
    private readonly AppDbContext _context;

    public FeedbackNotificationsViewComponent(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IViewComponentResult> InvokeAsync(
        int userId,
        bool isAnalyst)
    {
        if (userId <= 0)
        {
            return View(new FeedbackNotificationViewModel());
        }

        IQueryable<FeedbackMessage> unread = _context.FeedbackMessages
            .AsNoTracking()
            .Where(message => !message.IsRead);

        isAnalyst = isAnalyst || await unread.AnyAsync(message =>
            message.WorkItem.AnalystId == userId &&
            message.SenderUserId != userId);

        unread = isAnalyst
            ? unread.Where(message =>
                message.WorkItem.AnalystId == userId &&
                message.SenderUserId != userId)
            : unread.Where(message =>
                message.WorkItem.CreatedByUserId == userId &&
                message.SenderUserId != userId);

        int unreadCount = await unread.CountAsync();
        List<FeedbackNotificationItemViewModel> items = await unread
            .OrderByDescending(message => message.CreatedAt)
            .ThenByDescending(message => message.Id)
            .Take(10)
            .Select(message => new FeedbackNotificationItemViewModel
            {
                WorkItemId = message.WorkItemId,
                RequestNumber = message.WorkItem.RequestNumber,
                SenderName = message.SenderUser.FullName,
                Message = message.Message,
                CreatedAt = message.CreatedAt
            })
            .ToListAsync();

        return View(new FeedbackNotificationViewModel
        {
            UnreadCount = unreadCount,
            TargetController = isAnalyst
                ? "Analyst"
                : "ProjectManager",
            TargetAction = isAnalyst ? "Review" : "Details",
            Items = items
        });
    }
}
