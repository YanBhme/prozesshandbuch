import AppKit
let output = CommandLine.arguments[1]
let sizes = [16, 32, 128, 256, 512]
for size in sizes {
    for scale in [1, 2] {
        let pixels = size * scale
        let rep = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: pixels, pixelsHigh: pixels, bitsPerSample: 8, samplesPerPixel: 4, hasAlpha: true, isPlanar: false, colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0)!
        NSGraphicsContext.saveGraphicsState(); NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: rep)
        let c = NSGraphicsContext.current!.cgContext
        c.scaleBy(x: CGFloat(pixels)/1024, y: CGFloat(pixels)/1024)
        c.setFillColor(NSColor.white.cgColor); c.addPath(CGPath(roundedRect: CGRect(x: 50, y: 50, width: 924, height: 924), cornerWidth: 200, cornerHeight: 200, transform: nil)); c.fillPath()
        c.translateBy(x: 180, y: 180); c.scaleBy(x: 664, y: 664)
        c.setFillColor(NSColor(srgbRed: 0, green: 23/255, blue: 67/255, alpha: 1).cgColor)
        c.fill(CGRect(x: 0, y: 0, width: 0.42, height: 0.435)); c.fill(CGRect(x: 0, y: 0.565, width: 0.42, height: 0.435))
        c.setFillColor(NSColor(srgbRed: 229/255, green: 84/255, blue: 17/255, alpha: 1).cgColor)
        c.fill(CGRect(x: 0.55, y: 0, width: 0.26, height: 1)); c.fill(CGRect(x: 0.81, y: 0, width: 0.19, height: 0.435)); c.fill(CGRect(x: 0.81, y: 0.565, width: 0.19, height: 0.435))
        NSGraphicsContext.restoreGraphicsState()
        let suffix = scale == 2 ? "@2x" : ""
        try rep.representation(using: .png, properties: [:])!.write(to: URL(fileURLWithPath: output).appendingPathComponent("icon_\(size)x\(size)\(suffix).png"))
    }
}
