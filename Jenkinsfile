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
                        set -e

                        IMAGE_TAG="${BUILD_NUMBER}"

                        echo "$DOCKER_PASS" | docker login \
                            -u "$DOCKER_USER" --password-stdin

                        docker build \
                            --build-arg PROJECT_PATH=src/ApiGateway/Fundoo.ApiGateway/Fundoo.ApiGateway.csproj \
                            -t ${DOCKER_HUB_USER}/fundoo-gateway:${IMAGE_TAG} \
                            -t ${DOCKER_HUB_USER}/fundoo-gateway:live .

                        docker push ${DOCKER_HUB_USER}/fundoo-gateway:${IMAGE_TAG}
                        docker push ${DOCKER_HUB_USER}/fundoo-gateway:live

                        docker build \
                            --build-arg PROJECT_PATH=src/Services/User/Fundoo.User.Api/Fundoo.User.Api.csproj \
                            -t ${DOCKER_HUB_USER}/fundoo-user:${IMAGE_TAG} \
                            -t ${DOCKER_HUB_USER}/fundoo-user:live .

                        docker push ${DOCKER_HUB_USER}/fundoo-user:${IMAGE_TAG}
                        docker push ${DOCKER_HUB_USER}/fundoo-user:live

                        docker build \
                            --build-arg PROJECT_PATH=src/Services/Notes/Fundoo.Notes.Api/Fundoo.Notes.Api.csproj \
                            -t ${DOCKER_HUB_USER}/fundoo-notes:${IMAGE_TAG} \
                            -t ${DOCKER_HUB_USER}/fundoo-notes:live .

                        docker push ${DOCKER_HUB_USER}/fundoo-notes:${IMAGE_TAG}
                        docker push ${DOCKER_HUB_USER}/fundoo-notes:live

                        docker build \
                            --build-arg PROJECT_PATH=src/Services/Notification/Fundoo.Notification.Api/Fundoo.Notification.Api.csproj \
                            -t ${DOCKER_HUB_USER}/fundoo-notification:${IMAGE_TAG} \
                            -t ${DOCKER_HUB_USER}/fundoo-notification:live .

                        docker push ${DOCKER_HUB_USER}/fundoo-notification:${IMAGE_TAG}
                        docker push ${DOCKER_HUB_USER}/fundoo-notification:live

                        docker logout
                    '''
                }
            }
        }

        stage('Deploy to Kubernetes') {
            steps {
                sh '''
                    set -e

                    IMAGE_TAG="${BUILD_NUMBER}"

                    kubectl --kubeconfig=/var/jenkins_home/.kube/config \
                        set image deployment/gateway-deploy \
                        gateway=${DOCKER_HUB_USER}/fundoo-gateway:${IMAGE_TAG}

                    kubectl --kubeconfig=/var/jenkins_home/.kube/config \
                        set image deployment/user-deploy \
                        user=${DOCKER_HUB_USER}/fundoo-user:${IMAGE_TAG}

                    kubectl --kubeconfig=/var/jenkins_home/.kube/config \
                        set image deployment/notes-deploy \
                        notes=${DOCKER_HUB_USER}/fundoo-notes:${IMAGE_TAG}

                    kubectl --kubeconfig=/var/jenkins_home/.kube/config \
                        set image deployment/notification-deploy \
                        notification=${DOCKER_HUB_USER}/fundoo-notification:${IMAGE_TAG}
                '''
            }
        }

        stage('Verify Kubernetes Rollout') {
            steps {
                sh '''
                    set -e

                    kubectl --kubeconfig=/var/jenkins_home/.kube/config \
                        rollout status deployment/gateway-deploy --timeout=180s

                    kubectl --kubeconfig=/var/jenkins_home/.kube/config \
                        rollout status deployment/user-deploy --timeout=180s

                    kubectl --kubeconfig=/var/jenkins_home/.kube/config \
                        rollout status deployment/notes-deploy --timeout=180s

                    kubectl --kubeconfig=/var/jenkins_home/.kube/config \
                        rollout status deployment/notification-deploy --timeout=180s
                '''
            }
        }
    }

    post {
        success {
            echo 'Fundoo CI/CD completed successfully!'
        }

        failure {
            echo 'Pipeline failed. Check the Jenkins console output.'
        }
    }
}
