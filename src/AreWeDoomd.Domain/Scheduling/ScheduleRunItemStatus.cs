namespace AreWeDoomd.Domain.Scheduling;

public enum ScheduleRunItemStatus { AwaitingLlm = 0, Completed = 1, BelowThreshold = 2, Failed = 3, Superseded = 4 }
