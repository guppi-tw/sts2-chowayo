import Foundation
import ScreenCaptureKit
import AVFoundation
import CoreGraphics
import AppKit

enum CaptureFailure: Error { case message(String) }

final class GameCapture: NSObject, SCStreamOutput, SCStreamDelegate {
    let folder: URL
    let writer: AVAssetWriter
    let video: AVAssetWriterInput
    let audio: AVAssetWriterInput
    let queue = DispatchQueue(label: "Chowayo.game.capture")
    var firstPTS: CMTime?
    var firstUTC: Double?
    var videoFrames = 0
    var audioBuffers = 0
    var droppedFrames = 0
    var stopping = false
    var streamError: Error?

    init(folder: URL) throws {
        self.folder = folder
        writer = try AVAssetWriter(outputURL: folder.appendingPathComponent("game-system.mov"), fileType: .mov)
        video = AVAssetWriterInput(mediaType: .video, outputSettings: [
            AVVideoCodecKey: AVVideoCodecType.h264,
            AVVideoWidthKey: 1920, AVVideoHeightKey: 1080,
            AVVideoCompressionPropertiesKey: [
                AVVideoAverageBitRateKey: 12_000_000,
                AVVideoExpectedSourceFrameRateKey: 60,
                AVVideoMaxKeyFrameIntervalKey: 120,
                AVVideoAllowFrameReorderingKey: false,
            ],
        ])
        audio = AVAssetWriterInput(mediaType: .audio, outputSettings: [
            AVFormatIDKey: kAudioFormatMPEG4AAC,
            AVSampleRateKey: 48_000, AVNumberOfChannelsKey: 2,
            AVEncoderBitRateKey: 256_000,
        ])
        video.expectsMediaDataInRealTime = true
        audio.expectsMediaDataInRealTime = true
        super.init()
        writer.add(video)
        writer.add(audio)
        guard writer.startWriting() else { throw writer.error ?? CaptureFailure.message("Could not start writer") }
    }

    func stream(_ stream: SCStream, didStopWithError error: Error) {
        if !stopping { streamError = error }
    }

    func stream(_ stream: SCStream, didOutputSampleBuffer sample: CMSampleBuffer, of type: SCStreamOutputType) {
        guard !stopping, sample.isValid, writer.status == .writing else { return }
        let pts = CMSampleBufferGetPresentationTimeStamp(sample)
        if type == .screen {
            guard let attachments = CMSampleBufferGetSampleAttachmentsArray(sample, createIfNecessary: false) as? [[SCStreamFrameInfo: Any]],
                  let status = attachments.first?[.status] as? Int,
                  status == SCFrameStatus.complete.rawValue else { return }
            if firstPTS == nil {
                firstPTS = pts
                firstUTC = Date().timeIntervalSince1970 -
                    CMTimeGetSeconds(CMTimeSubtract(CMClockGetTime(CMClockGetHostTimeClock()), pts))
                writer.startSession(atSourceTime: pts)
                try? writeMetadata(name: "capture-ready.json")
            }
            if video.isReadyForMoreMediaData {
                if video.append(sample) { videoFrames += 1 }
            } else { droppedFrames += 1 }
        } else if type == .audio, let start = firstPTS, pts >= start {
            if audio.isReadyForMoreMediaData, audio.append(sample) { audioBuffers += 1 }
        }
    }

    func writeMetadata(name: String) throws {
        let object: [String: Any] = [
            "firstFrameUTC": firstUTC ?? 0,
            "firstPTS": firstPTS.map(CMTimeGetSeconds) ?? 0,
            "videoFrames": videoFrames, "audioBuffers": audioBuffers,
            "droppedVideoFrames": droppedFrames,
            "audioScope": "com.megacrit.SlayTheSpire2", "microphone": false,
        ]
        try JSONSerialization.data(withJSONObject: object, options: [.prettyPrinted, .sortedKeys])
            .write(to: folder.appendingPathComponent(name), options: .atomic)
    }

    func finish() async throws {
        await withCheckedContinuation { (continuation: CheckedContinuation<Void, Never>) in
            queue.async {
                self.stopping = true
                self.video.markAsFinished()
                self.audio.markAsFinished()
                self.writer.finishWriting { continuation.resume() }
            }
        }
        guard writer.status == .completed, videoFrames > 60, audioBuffers > 10 else {
            throw writer.error ?? CaptureFailure.message("Incomplete video or game audio")
        }
        try writeMetadata(name: "capture-finished.json")
        try Data("Capture complete".utf8).write(to: folder.appendingPathComponent("capture-finished"))
    }
}

@main
struct CaptureMain {
    @MainActor
    static func main() async {
        do {
            _ = NSApplication.shared
            NSApp.setActivationPolicy(.prohibited)
            guard CommandLine.arguments.count == 3 else { throw CaptureFailure.message("Usage: CaptureGame folder game-pid") }
            guard CGPreflightScreenCaptureAccess() else { throw CaptureFailure.message("Screen recording permission is not available") }
            let folder = URL(fileURLWithPath: CommandLine.arguments[1], isDirectory: true)
            guard let pid = Int32(CommandLine.arguments[2]) else { throw CaptureFailure.message("Invalid game PID") }
            let content = try await SCShareableContent.excludingDesktopWindows(true, onScreenWindowsOnly: false)
            guard let window = content.windows.first(where: {
                $0.owningApplication?.processID == pid &&
                $0.owningApplication?.bundleIdentifier == "com.megacrit.SlayTheSpire2" &&
                $0.frame.width > 600 && $0.frame.height > 400
            }) else { throw CaptureFailure.message("Game window not found") }
            let configuration = SCStreamConfiguration()
            configuration.width = 1920
            configuration.height = 1080
            configuration.minimumFrameInterval = CMTime(value: 1, timescale: 60)
            configuration.queueDepth = 6
            configuration.showsCursor = false
            configuration.capturesAudio = true
            configuration.excludesCurrentProcessAudio = true
            configuration.sampleRate = 48_000
            configuration.channelCount = 2
            configuration.ignoreShadowsSingleWindow = true
            if #available(macOS 15.0, *) { configuration.captureMicrophone = false }
            let capture = try GameCapture(folder: folder)
            let stream = SCStream(filter: SCContentFilter(desktopIndependentWindow: window),
                                  configuration: configuration, delegate: capture)
            try stream.addStreamOutput(capture, type: .screen, sampleHandlerQueue: capture.queue)
            try stream.addStreamOutput(capture, type: .audio, sampleHandlerQueue: capture.queue)
            try await stream.startCapture()
            let deadline = Date().addingTimeInterval(180)
            while !FileManager.default.fileExists(atPath: folder.appendingPathComponent("capture-stop").path) {
                if let error = capture.streamError { throw error }
                if let error = capture.writer.error { throw error }
                guard Date() < deadline else { throw CaptureFailure.message("Recording timed out") }
                try await Task.sleep(nanoseconds: 100_000_000)
            }
            capture.stopping = true
            try await stream.stopCapture()
            try await capture.finish()
            print("PASS: game window, game audio and no microphone; \(capture.videoFrames) video frames, \(capture.audioBuffers) audio buffers")
        } catch {
            FileHandle.standardError.write(Data("CAPTURE FAIL: \(error)\n".utf8))
            exit(1)
        }
    }
}
