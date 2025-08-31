namespace Castellan.Worker.Configuration;

public static class EditionFeatures
{
    public static class Features
    {
        // Castellan core features (all available)
        public static bool LocalMonitoring => true;
        public static bool BasicAlerting => true;
        public static bool VectorDatabase => true;
        public static bool AIThreatDetection => true;
        public static bool CommunitySupport => true;
        public static bool BasicCompliance => true;
        public static bool SecurityEventDetection => true;
        public static bool BasicAuditLogging => true;
        public static bool BasicEncryption => true;
        public static bool IPEnrichment => true;
        public static bool PerformanceMonitoring => true;
        public static bool NotificationServices => true;
        public static bool WindowsEventLogs => true;
        public static bool PowerShellMonitoring => true;
        public static bool CorrelationEngine => true;
    }

    public static string GetEditionName() => "Castellan";

    public static string GetVersionString()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString() ?? "Unknown";
        return $"{GetEditionName()} v{version}";
    }
}
