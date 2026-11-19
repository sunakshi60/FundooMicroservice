
pipeline {
    agent any

    environment {
        DOCKER_HUB_USER = 'sunakshii'
    }

    stages {

        stage('Checkout Code') {
            steps {
                echo 'Checking out FundooMicroservice repository'
                checkout scm
            }
        }

        stage('Build and Push Microservices') {
            steps {
                withCredentials([
                    usernamePassword(
                        credentialsId: 'docker-hub-creds',
                        usernameVariable: 'DOCKER_USER',
                        passwordVariable: 'DOCKER_PASS'
                    )
                ]) {
                    sh '''
                        echo "$DOCKER_PASS" | docker login \
                            -u "$DOCKER_USER" --password-stdin

                        docker build \
                            --build-arg PROJECT_PATH=src/ApiGateway/Fundoo.ApiGateway/Fundoo.ApiGateway.csproj \
                            -t ${DOCKER_HUB_USER}/fundoo-gateway:live .

                        docker push ${DOCKER_HUB_USER}/fundoo-gateway:live

                        docker build \
                            --build-arg PROJECT_PATH=src/Services/User/Fundoo.User.Api/Fundoo.User.Api.csproj \
                            -t ${DOCKER_HUB_USER}/fundoo-user:live .

                        docker push ${DOCKER_HUB_USER}/fundoo-user:live

                        docker build \
                            --build-arg PROJECT_PATH=src/Services/Notes/Fundoo.Notes.Api/Fundoo.Notes.Api.csproj \
                            -t ${DOCKER_HUB_USER}/fundoo-notes:live .

                        docker push ${DOCKER_HUB_USER}/fundoo-notes:live

                        docker build \
                            --build-arg PROJECT_PATH=src/Services/Notification/Fundoo.Notification.Api/Fundoo.Notification.Api.csproj \
                            -t ${DOCKER_HUB_USER}/fundoo-notification:live .

                        docker push ${DOCKER_HUB_USER}/fundoo-notification:live

                        docker logout
                    '''
                }
            }
        }
    }

    post {
        success {
            echo 'All four Fundoo microservice images built and pushed successfully!'
        }

        failure {
            echo 'Pipeline failed. Check the Jenkins console output.'
        }
    }
}
