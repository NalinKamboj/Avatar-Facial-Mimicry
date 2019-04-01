import sys
import os
import dlib
import time
import cv2
import imutils
from imutils.video import VideoStream
from imutils import face_utils
import json
import socket
import numpy as np
from keras.preprocessing.image import img_to_array
from keras.models import load_model
# import imutils.video.videostream

# Set up comms
UDP_IP = "127.0.0.1"
UDP_PORT = 5065
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
last = []  # No clue what this is. XD

# Add a check for commandline predictor path, maybe...
dirname = os.path.dirname(__file__)
predictor_path = os.path.join(dirname, "shape_predictor_68_face_landmarks.dat")
emotion_model_path = os.path.join(dirname, "emotion_model.hdf5")

# Set initial parameters before beginning detections...
camPoints = [6.5308391993466671e+002, 0.0, 3.1950000000000000e+002,
             0.0, 6.5308391993466671e+002, 2.3950000000000000e+002, 0.0, 0.0, 1.0]
distCoeffs = [7.0834633684407095e-002, 6.9140193737175351e-002,
              0.0, 0.0, -1.3073460323689292e+000]
cameraMatrix = np.array(camPoints).reshape(3, 3).astype(np.float32)
distCoeffs = np.array(distCoeffs).reshape(5, 1).astype(np.float32)
initialObjPts = np.float32([[6.825897, 6.760612, 4.402142],
                            [1.330353, 7.122144, 6.903745],
                            [-1.330353, 7.122144, 6.903745],
                            [-6.825897, 6.760612, 4.402142],
                            [5.311432, 5.485328, 3.987654],
                            [1.789930, 5.393625, 4.413414],
                            [-1.789930, 5.393625, 4.413414],
                            [-5.311432, 5.485328, 3.987654],
                            [2.005628, 1.409845, 6.165652],
                            [-2.005628, 1.409845, 6.165652],
                            [2.774015, -2.080775, 5.048531],
                            [-2.774015, -2.080775, 5.048531],
                            [0.000000, -3.116408, 6.097667],
                            [0.000000, -7.415691, 4.070434]])
boxInitial = np.float32([[10.0, 10.0, 10.0],
                         [10.0, 10.0, -10.0],
                         [10.0, -10.0, -10.0],
                         [10.0, -10.0, 10.0],
                         [-10.0, 10.0, 10.0],
                         [-10.0, 10.0, -10.0],
                         [-10.0, -10.0, -10.0],
                         [-10.0, -10.0, 10.0]])
box_lines = [[0, 1], [1, 2], [2, 3], [3, 0],
             [4, 5], [5, 6], [6, 7], [7, 4],
             [0, 4], [1, 5], [2, 6], [3, 7]]
EMOTIONS = ["angry", "disgust", "scared",
            "happy", "sad", "surprised", "neutral"]
emotion_detector = load_model(emotion_model_path, compile=False)


def estimatePose(data):
    startPoints = np.float32([data[17], data[21], data[22], data[26], data[36],
                              data[39], data[42], data[45], data[31], data[35],
                              data[48], data[54], data[57], data[8]])
    # Solve PnP to get 3D points...
    _, rotationVector, translationVector = cv2.solvePnP(
        initialObjPts, startPoints, cameraMatrix, distCoeffs)
    boxFinal, _ = cv2.projectPoints(
        boxInitial, rotationVector, translationVector, cameraMatrix, distCoeffs)
    boxFinal = tuple(map(tuple, boxFinal.reshape(8, 2)))

    # Calculate Euler Angle
    rotationMatrix, _ = cv2.Rodrigues(rotationVector)
    poseMatrix = cv2.hconcat((rotationMatrix, translationVector))
    _, _, _, _, _, _, eulerAngle = cv2.decomposeProjectionMatrix(poseMatrix)

    return boxFinal, eulerAngle


def main():
    if not os.path.exists(predictor_path):
        print("File does not exist")
        exit()

    print("[INFO] Loading Face Landmark Detector Model.")
    detector = dlib.get_frontal_face_detector()
    predictor = dlib.shape_predictor(predictor_path)
    # window = dlib.image_window()
    print("[INFO] Waiting for camera.")
    vstream = VideoStream().start()
    time.sleep(2.0)

    # Inform client that server is now running...
    # sock.sendto("1".encode(), (UDP_IP, UDP_PORT))
    print("[INFO] Running!")

    # Actual program loop for landmarking and emotion detection
    while (True):
        frame = vstream.read()
        frame_gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)

        # detect face's bounding rectangle(s)
        face_rects = detector(frame_gray, 0)

        if len(face_rects) > 0:
            displayText = "{} face(s) found".format(len(face_rects))
            cv2.putText(frame, displayText, (10, 20),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.5, (255, 0, 0), 2)

        # JSONs for holding data
        mainJSON = {}
        lmJSONList = []
        eulerJSON = {}

        for rect in face_rects:
            # print("RECT: {}".format(rect))
            left = rect.left()
            top = rect.top()
            right = rect.right()
            bottom = rect.bottom()
            # print("Face points - Left: {} Top: {} Right: {} Bottom: {}".format(left, top, right, bottom))

            # Extract face for recognizing emotions...
            face = frame_gray[top:bottom, left:right]
            face = cv2.resize(face, (48, 48))
            face = face.astype("float")/255.0
            face = img_to_array(face)
            face = np.expand_dims(face, axis=0)

            # Detect emotion
            emotion_preds = emotion_detector.predict(face)[0]
            emotion_prob = np.max(emotion_preds)
            emotion_name = EMOTIONS[emotion_preds.argmax()]
            emotion_label = emotion_name + \
                " - " + str(emotion_prob)
            cv2.putText(frame, emotion_label, (left, left - 20),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.45, (0, 0, 255), 2)
            # print("FACE: {}".format(face))
            # cv2.imshow("FACE", face)
            shape = predictor(frame_gray, rect)
            shape = face_utils.shape_to_np(shape)
            boxLocation, boxAngle = estimatePose(shape)

            # Assemble Euler Angles JSON
            eulerJSON["x"] = boxAngle[0][0]
            eulerJSON["y"] = boxAngle[1][0]
            eulerJSON["z"] = boxAngle[2][0]

            # Assemble Landmarks JSON
            counter = 0
            for data in shape.tolist():
                # print(">>>>>>>>>")
                landmarkJSON = {}
                for data2 in data:
                    if counter == 0:
                        landmarkJSON["x"] = data2
                        counter = counter + 1
                        # print("\t>>>>: {}".format(counter))
                    else:
                        landmarkJSON["y"] = data2
                        counter = counter - 1
                        # print("\t<<<<: {}".format(counter))
                lmJSONList.append(landmarkJSON)

            # Create final JSON
            mainJSON["eulerAngles"] = eulerJSON
            mainJSON["landmarks"] = lmJSONList
            mainJSON["emotion"] = emotion_name
            mainJSON["confidence"] = float(emotion_prob)
            # Dump JSON to string
            faceData = json.dumps(mainJSON)
            # print(faceData)

            for start, end in box_lines:
                cv2.line(frame, boxLocation[start],
                         boxLocation[end], (0, 255, 0))
            for(x, y) in shape:
                cv2.circle(frame, (x, y), 1, (0, 255, 0), -1)

            if(len(face_rects) == 1):
                sock.sendto(faceData.encode(), (UDP_IP, UDP_PORT))
                # print("[INFO] Data sent!")

        # showing results
        cv2.imshow("Result frame", frame)
        key = cv2.waitKey(1) & 0xFF

        if key == ord("q"):
            break
    # Perform cleanup
    print("[INFO] Cleaning up and quitting :(")
    cv2.destroyAllWindows()
    vstream.stop()


if __name__ == '__main__':
    main()


# OLD CODE
# boxLocationList = list(map(list, boxLocation))
# for index, _ in enumerate(boxLocationList):
#     for index2, item2 in enumerate(boxLocationList[index]):
#         boxLocationList[index][index2] = float(item2)
# map(float, boxLocationList)
# faceData["boxPoints"] = boxLocationList
