using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using SwiftyS26.Models;

namespace SwiftyS26.Services
{
    public class UncollectedOrderService
    {
        private readonly GymHubDB db;

        public UncollectedOrderService(GymHubDB database)
        {
            db = database ?? throw new ArgumentNullException(nameof(database));
        }

        public void ProcessDueOrders()
        {
            var now = DateTime.Now;
            var ids = db.StoreOrders
                .Where(o => o.Status == "ReadyForPickup" &&
                            o.ReadyAt.HasValue &&
                            o.PickupDeadline.HasValue)
                .Select(o => o.StoreOrderId)
                .ToList();

            foreach (var id in ids)
            {
                try
                {
                    ProcessOneOrder(id, now);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        "[UNCOLLECTED PROCESS ERROR] Order " + id + ": " + ex);
                }
            }
        }

        private void ProcessOneOrder(int orderId, DateTime now)
        {
            var order = db.StoreOrders
                .Include(o => o.Member)
                .Include(o => o.Items.Select(i => i.Product))
                .FirstOrDefault(o => o.StoreOrderId == orderId);

            if (order == null || order.Status != "ReadyForPickup" ||
                !order.ReadyAt.HasValue || !order.PickupDeadline.HasValue)
                return;

            if (now >= order.PickupDeadline.Value)
            {
                ExpireOrder(order, now);
                return;
            }

            var hours = (now - order.ReadyAt.Value).TotalHours;

            if (hours >= 96 && !order.FinalReminderSentAt.HasValue)
            {
                order.FinalReminderSentAt = now;
                AddEvent(order, "FinalReminderSent",
                    "Final pickup reminder recorded.", "System", "GymHub");
                db.SaveChanges();
                SendFinalReminder(order);
                return;
            }

            if (hours >= 48 && !order.FirstReminderSentAt.HasValue)
            {
                order.FirstReminderSentAt = now;
                AddEvent(order, "FirstReminderSent",
                    "First pickup reminder recorded.", "System", "GymHub");
                db.SaveChanges();
                SendFirstReminder(order);
            }
        }

        private void ExpireOrder(StoreOrder order, DateTime now)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    // Re-check inside the transaction.
                    if (order.Status != "ReadyForPickup") return;

                    order.Status = "UncollectedPendingReview";
                    order.UncollectedAt = now;
                    order.UncollectedResolutionStatus = "PendingAdminReview";

                    if (order.IsPickupStockReserved)
                    {
                        foreach (var item in order.Items)
                        {
                            if (item.Product == null) continue;

                            item.Product.QuantityOnHand += item.Quantity;
                            db.InventoryMovements.Add(new InventoryMovement
                            {
                                ProductId = item.ProductId,
                                QuantityChange = item.Quantity,
                                MovementType = "UncollectedOrderRelease",
                                ReferenceType = "StoreOrder",
                                ReferenceId = order.StoreOrderId,
                                ReferenceNumber = order.ReferenceNumber,
                                Notes = "Reserved stock released after pickup deadline.",
                                PerformedBy = "GymHub System",
                                CreatedAt = now
                            });
                        }

                        order.IsPickupStockReserved = false;
                        order.StockReleasedAt = now;
                    }

                    AddEvent(order, "MarkedUncollected",
                        "Pickup deadline expired. Order locked and sent for Admin review.",
                        "System", "GymHub");

                    db.SaveChanges();
                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }

            SendUncollectedNotifications(order);
        }

        public ServiceResult ExtendDeadline(
            int orderId, DateTime newDeadline, string reason, string actor)
        {
            if (newDeadline <= DateTime.Now)
                return ServiceResult.Fail("The new deadline must be in the future.");

            if (string.IsNullOrWhiteSpace(reason))
                return ServiceResult.Fail("An extension reason is required.");

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var order = db.StoreOrders
                        .Include(o => o.Member)
                        .Include(o => o.Items.Select(i => i.Product))
                        .FirstOrDefault(o => o.StoreOrderId == orderId);

                    if (order == null) return ServiceResult.Fail("Order not found.");
                    if (order.Status != "UncollectedPendingReview")
                        return ServiceResult.Fail("Only an uncollected order awaiting review can be extended.");

                    var unavailable = order.Items
                        .Where(i => i.Product == null ||
                                    !i.Product.IsActive ||
                                    i.Product.QuantityOnHand < i.Quantity)
                        .Select(i => i.Product == null ? "Unknown product" : i.Product.Name)
                        .ToList();

                    if (unavailable.Any())
                        return ServiceResult.Fail(
                            "The order cannot be reopened because stock is unavailable: " +
                            string.Join(", ", unavailable));

                    foreach (var item in order.Items)
                    {
                        item.Product.QuantityOnHand -= item.Quantity;
                        db.InventoryMovements.Add(new InventoryMovement
                        {
                            ProductId = item.ProductId,
                            QuantityChange = -item.Quantity,
                            MovementType = "PickupExtensionReservation",
                            ReferenceType = "StoreOrder",
                            ReferenceId = order.StoreOrderId,
                            ReferenceNumber = order.ReferenceNumber,
                            Notes = reason,
                            PerformedBy = actor
                        });
                    }

                    order.PreviousPickupDeadline = order.PickupDeadline;
                    order.PickupDeadline = newDeadline;
                    order.ReadyAt = DateTime.Now;
                    order.FirstReminderSentAt = null;
                    order.FinalReminderSentAt = null;
                    order.DeadlineExtensionCount++;
                    order.Status = "ReadyForPickup";
                    order.UncollectedResolutionStatus = "DeadlineExtended";
                    order.UncollectedResolutionNotes = reason.Trim();
                    order.UncollectedResolvedBy = actor;
                    order.UncollectedResolvedAt = DateTime.Now;
                    order.IsPickupStockReserved = true;

                    AddEvent(order, "DeadlineExtended",
                        "Pickup deadline extended to " + newDeadline.ToString("dd MMM yyyy HH:mm") +
                        ". Reason: " + reason.Trim(), "Admin", actor);

                    db.SaveChanges();
                    transaction.Commit();

                    try
                    {
                        EmailService.SendPickupDeadlineExtended(
                            order.Member.Email, order.Member.FullName,
                            order.ReferenceNumber, newDeadline);
                        new NotificationService(db).Notify(
                            "Member", order.MemberId, "Pickup Deadline Extended",
                            "Your order " + order.ReferenceNumber +
                            " is available for collection until " +
                            newDeadline.ToString("dd MMM yyyy HH:mm") + ".",
                            "Info", "/Store/OrderConfirmation/" + order.StoreOrderId);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("[EXTENSION NOTIFY] " + ex);
                    }

                    return ServiceResult.Ok("The deadline was extended and stock was reserved again.");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    System.Diagnostics.Debug.WriteLine("[EXTENSION ERROR] " + ex);
                    return ServiceResult.Fail("The deadline could not be extended. No changes were saved.");
                }
            }
        }

        public ServiceResult ResolveWithoutRefund(int orderId, string reason, string actor)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return ServiceResult.Fail("A resolution reason is required.");

            var order = db.StoreOrders.Find(orderId);
            if (order == null) return ServiceResult.Fail("Order not found.");
            if (order.Status != "UncollectedPendingReview")
                return ServiceResult.Fail("This order is not awaiting uncollected-order review.");

            order.Status = "UncollectedResolved";
            order.UncollectedResolutionStatus = "ResolvedNoRefund";
            order.UncollectedResolutionNotes = reason.Trim();
            order.UncollectedResolvedBy = actor;
            order.UncollectedResolvedAt = DateTime.Now;

            AddEvent(order, "ResolvedNoRefund", reason.Trim(), "Admin", actor);
            db.SaveChanges();
            return ServiceResult.Ok("The uncollected order was resolved without a refund.");
        }

        private void AddEvent(StoreOrder order, string type, string description,
            string role, string actor)
        {
            db.StoreOrderCollectionEvents.Add(new StoreOrderCollectionEvent
            {
                StoreOrderId = order.StoreOrderId,
                EventType = type,
                Description = description,
                PerformedByRole = role,
                PerformedBy = actor,
                CreatedAt = DateTime.Now
            });
        }

        private void SendFirstReminder(StoreOrder order)
        {
            try
            {
                new NotificationService(db).Notify(
                    "Member", order.MemberId,
                    "Reminder - Order Waiting for Collection",
                    "Your order " + order.ReferenceNumber + " is still waiting for collection.",
                    "Info", "/Store/OrderConfirmation/" + order.StoreOrderId);
                EmailService.SendOrderFirstReminder(
                    order.Member.Email, order.Member.FullName, order.ReferenceNumber);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[FIRST REMINDER ERROR] " + ex);
            }
        }

        private void SendFinalReminder(StoreOrder order)
        {
            try
            {
                new NotificationService(db).Notify(
                    "Member", order.MemberId,
                    "Final Reminder - Collect Your Order",
                    "Your order " + order.ReferenceNumber +
                    " must be collected before " +
                    order.PickupDeadline.Value.ToString("dd MMM yyyy HH:mm") + ".",
                    "Warning", "/Store/OrderConfirmation/" + order.StoreOrderId);
                EmailService.SendOrderFinalReminder(
                    order.Member.Email, order.Member.FullName, order.ReferenceNumber);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[FINAL REMINDER ERROR] " + ex);
            }
        }

        private void SendUncollectedNotifications(StoreOrder order)
        {
            try
            {
                var notifications = new NotificationService(db);
                notifications.Notify(
                    "Member", order.MemberId, "Order Collection Deadline Passed",
                    "Order " + order.ReferenceNumber +
                    " was not collected in time and is awaiting resolution.",
                    "Warning", "/Store/OrderConfirmation/" + order.StoreOrderId);
                notifications.NotifyAdmin(
                    "Uncollected Order Requires Review",
                    "Order " + order.ReferenceNumber +
                    " passed its deadline. Stock was released and an Admin decision is required.",
                    "Warning", "/StoreAdmin/ReviewUncollectedOrder/" + order.StoreOrderId);
                notifications.NotifyAllStoreStaff(
                    "Order Marked Uncollected",
                    "Order " + order.ReferenceNumber + " passed its pickup deadline.",
                    "Warning", "/StoreAdmin/UncollectedOrders");
                EmailService.SendOrderUncollectedPendingReview(
                    order.Member.Email, order.Member.FullName, order.ReferenceNumber);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[UNCOLLECTED NOTIFY ERROR] " + ex);
            }
        }
    }

    public class ServiceResult
    {
        public bool Success { get; private set; }
        public string Message { get; private set; }
        public static ServiceResult Ok(string message) =>
            new ServiceResult { Success = true, Message = message };
        public static ServiceResult Fail(string message) =>
            new ServiceResult { Success = false, Message = message };
    }
}
