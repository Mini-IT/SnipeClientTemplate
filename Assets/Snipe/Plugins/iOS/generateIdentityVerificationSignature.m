
// https://gist.github.com/BastianBlokland/bbc02a407b05beaf3f55ead3dd10f808

#import <Foundation/Foundation.h>
#import <GameKit/GameKit.h>

typedef void (*IdentityVerificationSignatureCallback)(const char * publicKeyUrl, const char * signature, int signatureLength, const char * salt, int saltLength, const uint64_t timestamp, const char * error);

extern void generateIdentityVerificationSignature(IdentityVerificationSignatureCallback callback) {
    
    GKLocalPlayer * localPlayer = [GKLocalPlayer localPlayer];

    NSLog(@"LocalPlayer: %@", localPlayer.playerID);
    [localPlayer generateIdentityVerificationSignatureWithCompletionHandler:^(NSURL * publicKeyUrl, NSData * signature, NSData * salt, uint64_t timestamp, NSError * error) {

        NSLog(@"Received 'generateIdentityVerificationSignature' callback, error: %@", error.description);

        // Create a pool for releasing the resources we create
        @autoreleasepool {
            
            // PublicKeyUrl
            const char * publicKeyUrlCharPointer = NULL;
            if (publicKeyUrl != NULL)
            {
                const NSString * publicKeyUrlString = [[NSString alloc] initWithString:[publicKeyUrl absoluteString]];
                publicKeyUrlCharPointer = [publicKeyUrlString UTF8String];
            }

            // Signature
            const char * signatureBytes = [signature bytes];
            int signatureLength = (int)[signature length];

            // Salt
            const char * saltBytes = [salt bytes];
            int saltLength = (int)[salt length];

            // Error
            const NSString * errorString = error.description;
            const char * errorStringPointer = [errorString UTF8String];

            // Callback
            callback(publicKeyUrlCharPointer, signatureBytes, signatureLength, saltBytes, saltLength, timestamp, errorStringPointer);
        }
    }];
}