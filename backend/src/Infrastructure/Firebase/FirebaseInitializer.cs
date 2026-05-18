using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backend.Infrastructure.Firebase;

public static class FirebaseInitializer
{
    public static FirebaseMessaging Initialize(IOptions<FirebaseOptions> options, ILogger logger)
    {
        var opts = options.Value;

        if (FirebaseApp.DefaultInstance is not null)
            return FirebaseMessaging.DefaultInstance;

        GoogleCredential credential;

        if (!string.IsNullOrWhiteSpace(opts.ServiceAccountJson))
        {
            credential = GoogleCredential.FromJson(opts.ServiceAccountJson);
        }
        else
        {
            var path = opts.ServiceAccountPath ?? "firebase-key.json";
            if (!File.Exists(path))
            {
                logger.LogWarning(
                    "Firebase service account file not found at '{Path}'. Push notifications will be disabled.",
                    path);
                return null!;
            }
            credential = GoogleCredential.FromFile(path);
        }

        FirebaseApp.Create(new AppOptions
        {
            Credential = credential,
            ProjectId = opts.ProjectId,
        });

        logger.LogInformation("Firebase Admin SDK initialized (project: {Project})", opts.ProjectId);
        return FirebaseMessaging.DefaultInstance;
    }
}
