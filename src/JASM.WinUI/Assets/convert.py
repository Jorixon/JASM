import os, sys
from PIL import Image

for infile in os.listdir("./"):
    print(infile)
    
    f, e = os.path.splitext(infile)
    outfile = f + ".png"
    if infile != outfile:
        try:
            with Image.open(infile) as im:
                im.convert("RGBA").save(outfile)
        except OSError:
            print("cannot convert", infile)