# First-Run Onboarding Copy

## Status

This is draft copy for the planned setup wizard. The current desktop prototype does not provide this wizard, wake detection, speaker testing, provider setup, personality selection, credential entry, startup registration, or a complete voice exchange. Do not present these screens or promises as implemented behavior.

## Welcome

**Meet Hey Computer**

Hey Computer waits for a wake phrase on this PC, listens to one request, and speaks the answer. You choose which speech and AI services it uses.

## Privacy summary

**Your wake phrase stays on this computer**

While wake listening is enabled, your microphone is processed locally. Ordinary room audio is not continuously uploaded or saved. After you activate the assistant, the captured request may be sent to the services you select. History is off by default.

Actions: `Review details` · `Continue` · `Use push-to-talk only`

## Microphone permission

**Choose and test a microphone**

Windows may ask for microphone permission. Select the device you want Hey Computer to use, speak normally, and confirm that the level meter responds. You can change or fully disable the microphone later.

Actions: `Open Windows permission settings` · `Test microphone` · `Continue`

## Speaker test

**Choose where replies play**

Play a short sample and adjust the volume. Hey Computer will pause wake listening during its own speech in the first release.

Actions: `Play sample` · `Choose output` · `Continue`

## Provider selection

**Choose how your request is handled**

Each option must show whether it runs locally or in the cloud, which data it receives, whether an account or key is required, and where current pricing and retention terms can be reviewed. Custom endpoints receive conversation data and should be used only when trusted.

Actions: `Use Grok preset` · `Choose another provider` · `Configure local options`

## Credential entry

**Connect your account**

Your API key is stored with Windows protection for your user account. It is not written to ordinary settings or logs. Usage charges are billed by the provider you choose.

Actions: `Save and test key` · `How keys are stored` · `Back`

## Personality

**Choose how the assistant responds**

Pick a preset or customize brevity, humor, tone, and voice. Personality settings are separate from your provider, so you can switch services without losing them.

Actions: `Preview` · `Customize` · `Continue`

## Wake listening consent

**Enable “Hey Computer”?**

When enabled, the microphone remains active for local wake detection. A visible tray state shows when Hey Computer is waiting, recording, processing, speaking, or muted. You can instead keep wake listening off and use push-to-talk.

Actions: `Enable wake phrase` · `Use push-to-talk only`

## Startup choice

**Start after you sign in?**

This is optional and can be changed from the tray menu. Hey Computer never requires administrator access merely to start with your user session.

Actions: `Start at sign-in` · `Not now`

## Final test

**Try your first request**

Say “Hey Computer,” wait for the acknowledgement, and ask a short question. The screen should show exactly when recording begins and where the request is sent.

Actions: `Try it` · `Finish with microphone muted`
