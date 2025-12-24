// QCS.API/Hubs/NotificationHub.cs
using Microsoft.AspNetCore.SignalR;

namespace QCS.Application.Hubs
{
    // นี่คือ "ห้องสื่อสาร" ของเรา
    public class NotificationHub : Hub
    {
        // ฟังก์ชันนี้ client สามารถเรียกเพื่อเทสได้ (ปกติเราจะส่งจาก Controller/Service แทน)
        public async Task SendUpdate(string message)
        {
            await Clients.All.SendAsync("ReceiveUpdate", message);
        }
    }
}