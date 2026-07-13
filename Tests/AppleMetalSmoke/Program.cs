// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;

using var game = new AppleMetalSmokeGame();
game.Run();

if (game.PresentedFrameCount < AppleMetalSmokeGame.RequiredFrameCount)
    return 1;

Console.WriteLine($"Apple Metal smoke test presented {game.PresentedFrameCount} frames.");
return 0;
