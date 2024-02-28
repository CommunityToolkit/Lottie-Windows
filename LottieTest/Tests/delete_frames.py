import os, shutil, re

root = "."

keep_each_kth_frame = 5

for directory in os.listdir("."):
    directory = os.path.join(root, directory)
    if not os.path.isdir(directory):
        continue
    for inner_directory in os.listdir(directory):
        if inner_directory != 'FramesAll':
            continue
        original_directory = os.path.join(directory, inner_directory)
        if not os.path.isdir(original_directory):
            continue

        target_directory = os.path.join(directory, 'Frames')

        shutil.rmtree(target_directory)
        os.makedirs(target_directory)

        frames = list(filter(lambda x: re.match(r'frame\d+.png', x), os.listdir(original_directory)))[1::keep_each_kth_frame]

        for frame in frames:
            shutil.copyfile(os.path.join(original_directory, frame), os.path.join(target_directory, frame))
